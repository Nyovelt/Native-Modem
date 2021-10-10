using B83.Collections;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Native_Modem
{
    public class Modem
    {
        readonly Protocol protocol;

        public Modem(Protocol protocol)
        {
            this.protocol = protocol;
        }

        int ModulateFrame(BitArray bitArray, int bitOffset, float[] stream, int streamOffset)
        {
            int count = 0;
            int end = bitOffset + protocol.FrameSize;
            for (int i = bitOffset; i < end; i++)
            {
                if (i >= bitArray.Length)
                {
                    protocol.Zero.CopyTo(stream, count + streamOffset);
                }
                else if (bitArray[i])
                {
                    protocol.One.CopyTo(stream, count + streamOffset);
                }
                else
                {
                    protocol.Zero.CopyTo(stream, count + streamOffset);
                }
                count += protocol.SamplesPerBit;
            }

            return count;
        }

        public SampleStream Modulate(BitArray bitArray)
        {
            int frames = bitArray.Count / protocol.FrameSize;
            if (bitArray.Count % protocol.FrameSize != 0)
            {
                frames++;
            }
            float[] stream = new float[frames * (protocol.Header.Length + protocol.FrameSize * protocol.SamplesPerBit)];
            int sampleCount = 0;
            int bitCount = 0;
            for (int i = 0; i < frames; i++)
            {
                protocol.Header.CopyTo(stream, sampleCount);
                sampleCount += protocol.Header.Length;
                sampleCount += ModulateFrame(bitArray, bitCount, stream, sampleCount);
                bitCount += protocol.FrameSize;
            }
            if (sampleCount != stream.Length)
            {
                throw new Exception("stream length error!");
            }
            return new SampleStream(protocol.WaveFormat, stream);
        }

        const int DECODE_WAIT_SAMPLES = 200;
        static readonly float K1 = 63f / 64f;
        static readonly float K2 = 1f / 64f;

        enum DemodulateState
        {
            Sync,
            Decode
        }

        public List<BitArray> Demodulate(SampleStream sampleStream, float syncPowerThreshold)
        {
            List<BitArray> results = new List<BitArray>();
            float power = 0f;
            float[] powerDebug = new float[sampleStream.Length];
            int startIndex = -1;
            RingBuffer<float> syncBuffer = new RingBuffer<float>(protocol.Header.Length);
            for (int i = 0; i < protocol.Header.Length; i++)
            {
                syncBuffer.Add(0f);
            }
            float[] syncPowerDebug = new float[sampleStream.Length];
            float syncPowerLocalMax = 0f;

            List<float> decodeFrame = new List<float>(protocol.FrameSize * protocol.SamplesPerBit);

            DemodulateState state = DemodulateState.Sync;

            for (int i = 0; i < sampleStream.Length; i++)
            {
                float sample = sampleStream[i];
                power = power * K1 + sample * sample * K2;
                powerDebug[i] = power;

                switch (state)
                {
                    case DemodulateState.Sync:
                        syncBuffer.ReadAndRemoveNext();
                        syncBuffer.Add(sample);
                        float syncPower = 0f;
                        for (int j = 0; j < protocol.Header.Length; j++)
                        {
                            syncPower += syncBuffer[j] * protocol.Header[j];
                        }
                        syncPowerDebug[i] = syncPower;
                        
                        if (syncPower > power * 2f && syncPower > syncPowerLocalMax && syncPower > syncPowerThreshold)
                        {
                            syncPowerLocalMax = syncPower;
                            startIndex = i;
                        }
                        else if (startIndex != -1 && i - startIndex > DECODE_WAIT_SAMPLES)
                        {
                            syncPowerLocalMax = 0f;
                            for (int j = 0; j < protocol.Header.Length; j++)
                            {
                                syncBuffer.Add(0f);
                            }
                            state = DemodulateState.Decode;
                            decodeFrame.Clear();
                            for (int j = startIndex + 1; j <= i; j++)
                            {
                                decodeFrame.Add(sampleStream[j]);
                            }
                        }
                        break;

                    case DemodulateState.Decode:
                        decodeFrame.Add(sample);
                        if (decodeFrame.Count == decodeFrame.Capacity)
                        {
                            bool[] bits = new bool[protocol.FrameSize];
                            for (int j = 0; j < protocol.FrameSize; j++)
                            {
                                int index = j * protocol.SamplesPerBit;

                                int half = protocol.SamplesPerBit >> 1;
                                int quarter = half >> 1;
                                int end = half + quarter;
                                float sum = 0f;
                                for (int k = quarter; k < end; k++)
                                {
                                    sum += decodeFrame[index + k] * protocol.One[k];
                                }

                                bits[j] = sum > protocol.Threshold;
                            }

                            // can add error correction / detection here

                            results.Add(new BitArray(bits));

                            startIndex = -1;
                            state = DemodulateState.Sync;
                        }
                        break;

                    default:
                        break;
                }
            }

            return results;
        }
    }
}
