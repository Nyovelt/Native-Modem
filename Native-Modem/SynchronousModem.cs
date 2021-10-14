using B83.Collections;
using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using STH1123.ReedSolomon;

namespace Native_Modem
{
    public class SynchronousModem
    {
        enum ModemState
        {
            Idling,
            Running,
            Disposed
        }

        readonly Protocol protocol;
        readonly AsioOut asioOut;
        readonly SampleFIFO TxFIFO;
        readonly SampleFIFO RxFIFO;
        readonly int frameSampleCount;
        readonly Queue<BitArray> modulateQueue;

        readonly float[] buffer = new float[1024];
        readonly float[] preheater = new float[760];

        ModemState modemState;
        WaveFileWriter writer;

        public SynchronousModem(Protocol protocol, string driverName)
        {
            this.protocol = protocol;

            SinusoidalSignal preheaterSignal = new SinusoidalSignal(1f, 2000f);
            float time = 0f;
            float step = 1f / protocol.WaveFormat.SampleRate;
            for (int i = 0; i < preheater.Length; i++)
            {
                preheater[i] = preheaterSignal.Evaluate(time);
                time += step;
            }

            frameSampleCount = protocol.Header.Length + protocol.FrameSize * protocol.SamplesPerBit;
            TxFIFO = new SampleFIFO(protocol.WaveFormat, frameSampleCount << 1);
            RxFIFO = new SampleFIFO(protocol.WaveFormat, frameSampleCount);
            modulateQueue = new Queue<BitArray>();

            asioOut = new AsioOut(driverName);
            SetupAsioOut();
            asioOut.InitRecordAndPlayback(TxFIFO.ToWaveProvider(), protocol.WaveFormat.Channels, protocol.WaveFormat.SampleRate);

            modemState = ModemState.Idling;
        }

        public void Start(Action<BitArray> onFrameReceived, string saveRecordTo = null)
        {
            if (modemState != ModemState.Idling)
            {
                return;
            }

            modemState = ModemState.Running;
            _ = Modulate();
            _ = Demodulate(onFrameReceived, 0.35f);

            if (!string.IsNullOrEmpty(saveRecordTo))
            {
                writer = new WaveFileWriter(saveRecordTo, protocol.WaveFormat);
            }

            asioOut.AudioAvailable += OnAsioOutAudioAvailable;
            asioOut.Play();
        }

        public void Stop()
        {
            if (modemState != ModemState.Running)
            {
                return;
            }

            modemState = ModemState.Idling;

            asioOut.Stop();
            asioOut.AudioAvailable -= OnAsioOutAudioAvailable;

            if (writer != null)
            {
                writer.Dispose();
            }
        }

        public void Dispose()
        {
            if (modemState == ModemState.Running)
            {
                Stop();
            }
            asioOut.Dispose();
        }

        public void Transport(BitArray bitArray)
        {
            modulateQueue.Enqueue(bitArray);
        }

        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(buffer);
            if (!RxFIFO.AvailableFor(sampleCount))
            {
                Console.WriteLine("RxFIFO overflow!!!!!");
            }
            RxFIFO.Push(buffer, sampleCount);
            if (writer != null)
            {
                writer.WriteSamples(buffer, 0, sampleCount);
            }
        }

        void SetupAsioOut()
        {
            Console.WriteLine("Select input channel:");
            var inputChannels = asioOut.DriverInputChannelCount;
            for (int i = 0; i < inputChannels; i++)
            {
                Console.WriteLine($"Input channel {i}: {asioOut.AsioInputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            asioOut.InputChannelOffset = channel;
            Console.WriteLine($"Choosing the input channel: {asioOut.AsioInputChannelName(channel)}");

            var outputChannels = asioOut.DriverOutputChannelCount;
            Console.WriteLine("Select output channel:");
            for (int i = 0; i < outputChannels; i++)
            {
                Console.WriteLine($"Output channel {i}: {asioOut.AsioOutputChannelName(i)}");
            }
            int outChannel = int.Parse(Console.ReadLine());
            asioOut.ChannelOffset = outChannel;
            Console.WriteLine($"Choosing the output channel: {asioOut.AsioOutputChannelName(outChannel)}");
        }

        void ModulateFrame(BitArray bitArray, int bitOffset)
        {
            int end = bitOffset + protocol.FrameSize;
            for (int i = bitOffset; i < end; i++)
            {
                if (i >= bitArray.Length || !bitArray[i])
                {
                    TxFIFO.Push(protocol.Zero);
                }
                else
                {
                    TxFIFO.Push(protocol.One);
                }
            }
        }

        async Task Modulate()
        {
            if (modemState != ModemState.Running)
            {
                return;
            }
            else if (modulateQueue.Count != 0)
            {
                if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(preheater.Length), () => modemState != ModemState.Running))
                {
                    return;
                }
                TxFIFO.Push(preheater);
            }

            while (true)
            {
                if (modulateQueue.Count == 0)
                {
                    if (!await TaskUtilities.WaitUntilUnless(() => modulateQueue.Count > 0, () => modemState != ModemState.Running))
                    {
                        return;
                    }

                    if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(preheater.Length), () => modemState != ModemState.Running))
                    {
                        return;
                    }
                    TxFIFO.Push(preheater);
                }

                BitArray array = modulateQueue.Dequeue();

                int frames = array.Count / protocol.FrameSize;
                if (array.Count % protocol.FrameSize != 0)
                {
                    frames++;
                }

                int bitCount = 0;
                for (int i = 0; i < frames; i++)
                {
                    if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(frameSampleCount), () => modemState != ModemState.Running))
                    {
                        return;
                    }
                    TxFIFO.Push(protocol.Header);
                    ModulateFrame(array, bitCount);
                    bitCount += protocol.FrameSize;
                }
            }
        }

        const int DECODE_WAIT_SAMPLES = 200;
        static readonly float K1 = 63f / 64f;
        static readonly float K2 = 1f / 64f;

        enum DemodulateState
        {
            Sync,
            Decode
        }

        async Task Demodulate(Action<BitArray> onFrameReceived, float syncPowerThreshold)
        {
            float power = 0f;
            bool decode = false;
            RingBuffer<float> syncBuffer = new RingBuffer<float>(protocol.Header.Length);
            for (int i = 0; i < protocol.Header.Length; i++)
            {
                syncBuffer.Add(0f);
            }
            float syncPowerLocalMax = 0f;

            List<float> decodeFrame = new List<float>(protocol.FrameSize * protocol.SamplesPerBit);

            DemodulateState state = DemodulateState.Sync;

            while (true)
            {
                if (!await TaskUtilities.WaitUntilUnless(() => !RxFIFO.IsEmpty, () => modemState != ModemState.Running))
                {
                    return;
                }

                float sample = RxFIFO.Pop();
                power = power * K1 + sample * sample * K2;

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
                        //Console.WriteLine($"syncPower: {syncPower}, localMax: {syncPowerLocalMax}, threshold: {protocol.HeaderPower * syncPowerThreshold}");
                        if (syncPower > protocol.HeaderPower * syncPowerThreshold && syncPower > syncPowerLocalMax)
                        {
                            syncPowerLocalMax = syncPower;
                            decode = true;
                            decodeFrame.Clear();
                        }
                        else if (decode)
                        {
                            decodeFrame.Add(sample);
                            if (decodeFrame.Count > DECODE_WAIT_SAMPLES)
                            {
                                syncPowerLocalMax = 0f;
                                for (int j = 0; j < protocol.Header.Length; j++)
                                {
                                    syncBuffer.Add(0f);
                                }
                                state = DemodulateState.Decode;
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

                            onFrameReceived.Invoke(new BitArray(bits));

                            decode = false;
                            state = DemodulateState.Sync;
                        }
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
