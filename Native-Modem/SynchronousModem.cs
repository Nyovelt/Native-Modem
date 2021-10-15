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
        readonly Queue<byte[]> modulateQueue;

        readonly float[] buffer = new float[1024];
        readonly float[] preheater = new float[960];
        readonly int[] lengthBuffer;
        readonly int[] fullFrameBuffer;
        readonly int[] lengthBufferRx;
        readonly int[] fullFrameBufferRx;

        ModemState modemState;
        WaveFileWriter writer;

        public SynchronousModem(Protocol protocol, string driverName, string saveTransportTo = null, string saveRecordTo = null, string saveSyncPowerTo = null)
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

            TxFIFO = new SampleFIFO(protocol.WaveFormat, protocol.WaveFormat.SampleRate, saveTransportTo);
            RxFIFO = new SampleFIFO(protocol.WaveFormat, protocol.WaveFormat.SampleRate >> 1, saveRecordTo);
            modulateQueue = new Queue<byte[]>();
            lengthBuffer = new int[1 + protocol.LengthRedundancyBytes];
            fullFrameBuffer = new int[protocol.FrameMaxDataBytes + protocol.RedundancyBytes];
            lengthBufferRx = new int[1 + protocol.LengthRedundancyBytes];
            fullFrameBufferRx = new int[protocol.FrameMaxDataBytes + protocol.RedundancyBytes];

            asioOut = new AsioOut(driverName);
            SetupAsioOut();
            asioOut.InitRecordAndPlayback(TxFIFO.ToWaveProvider(), protocol.WaveFormat.Channels, protocol.WaveFormat.SampleRate);

            modemState = ModemState.Idling;
            
            if (!string.IsNullOrEmpty(saveSyncPowerTo))
            {
                writer = new WaveFileWriter(saveSyncPowerTo, protocol.WaveFormat);
            }
        }

        public void Start(Action<byte[]> onFrameReceived)
        {
            if (modemState != ModemState.Idling)
            {
                return;
            }

            modemState = ModemState.Running;

            _ = Modulate();
            _ = Demodulate(onFrameReceived, 0.20f);

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
        }

        public void Dispose()
        {
            if (modemState == ModemState.Running)
            {
                Stop();
            }
            asioOut.Dispose();
            TxFIFO.Dispose();
            RxFIFO.Dispose();
            if (writer != null)
            {
                writer.Dispose();
            }
        }

        public void Transport(byte[] data)
        {
            modulateQueue.Enqueue(data);
        }

        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(buffer);
            if (!RxFIFO.AvailableFor(sampleCount))
            {
                Console.WriteLine("RxFIFO overflow!!!!!");
            }
            RxFIFO.Push(buffer, sampleCount);
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

        void ModulateByte(int dataByte)
        {
            for (int j = 0; j < 8; j++)
            {
                if ((dataByte & 0x1) == 1)
                {
                    TxFIFO.Push(protocol.One);
                }
                else
                {
                    TxFIFO.Push(protocol.Zero);
                }
                dataByte >>= 1;
            }
        }

        void ModulateFrame(int[] lengthWithECCInfo, int[] dataWithECCInfo)
        {
            // 1.Header: fixed size
            TxFIFO.Push(protocol.Header);

            // 2.DataLength and ECC: 8bits + 16bits
            foreach (int data in lengthWithECCInfo)
            {
                ModulateByte(data);
            }

            // 3.Data and ECC: varying size (ECC size fixed)
            foreach (int data in dataWithECCInfo)
            {
                ModulateByte(data);
            }
        }

        async Task Modulate()
        {
            if (modemState != ModemState.Running)
            {
                return;
            }

            ReedSolomonEncoder encoder = new ReedSolomonEncoder(protocol.GaloisField);

            if (modulateQueue.Count != 0)
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

                byte[] array = modulateQueue.Dequeue();

                int fullFrames = array.Length / protocol.FrameMaxDataBytes;
                int byteCounter = 0;
                lengthBuffer[0] = protocol.FrameMaxDataBytes;
                encoder.Encode(lengthBuffer, protocol.LengthRedundancyBytes);
                for (int i = 0; i < fullFrames; i++)
                {
                    int j = 0;
                    for (; j < protocol.FrameMaxDataBytes; j++)
                    {
                        fullFrameBuffer[j] = array[byteCounter++];
                    }
                    // May need to clear the paddings here
                    encoder.Encode(fullFrameBuffer, protocol.RedundancyBytes);
                    if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.FullFrameSampleCount), () => modemState != ModemState.Running))
                    {
                        return;
                    }
                    ModulateFrame(lengthBuffer, fullFrameBuffer);
                }

                int remainBytes = array.Length - byteCounter;
                lengthBuffer[0] = remainBytes;
                encoder.Encode(lengthBuffer, protocol.LengthRedundancyBytes);
                if (remainBytes != 0)
                {
                    int[] buffer = new int[remainBytes + protocol.RedundancyBytes];
                    int i = 0;
                    for (; i < remainBytes; i++)
                    {
                        buffer[i] = array[byteCounter++];
                    }
                    // May need to clear the paddings here
                    encoder.Encode(buffer, protocol.RedundancyBytes);
                    int sampleCount = protocol.Header.Length + protocol.SamplesPerByte * buffer.Length;
                    if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.FullFrameSampleCount), () => modemState != ModemState.Running))
                    {
                        return;
                    }
                    ModulateFrame(lengthBuffer, buffer);
                }
            }
        }

        int DemodulateByte(List<float> samples, ref int offset)
        {
            int ret = 0;
            for (int i = 0; i < 8; i++)
            {
                float sum = 0f;
                for (int j = 0; j < protocol.SamplesPerBit; j++)
                {
                    sum += samples[offset++] * protocol.One[j];
                }
                ret |= (sum > protocol.Threshold ? 1 : 0) << i;
            }
            return ret;
        }

        void DemodulateBytes(List<float> samples, ref int offset, int byteCount, int[] output)
        {
            for (int i = 0; i < byteCount; i++)
            {
                int byteTemp = 0;
                for (int j = 0; j < 8; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < protocol.SamplesPerBit; k++)
                    {
                        sum += samples[offset++] * protocol.One[k];
                    }
                    byteTemp |= (sum > protocol.Threshold ? 1 : 0) << j;
                }
                output[i] = byteTemp;
            }
        }

        const int DECODE_WAIT_SAMPLES = 400;

        enum DemodulateState
        {
            Sync,
            DecodeLength,
            DecodeData
        }

        async Task Demodulate(Action<byte[]> onFrameReceived, float syncPowerThreshold)
        {
            if (modemState != ModemState.Running)
            {
                return;
            }

            ReedSolomonDecoder decoder = new ReedSolomonDecoder(protocol.GaloisField);

            bool decode = false;
            RingBuffer<float> syncBuffer = new RingBuffer<float>(protocol.Header.Length);
            for (int i = 0; i < protocol.Header.Length; i++)
            {
                syncBuffer.Add(0f);
            }
            float syncPowerLocalMax = 0f;
            float minSyncPower = syncPowerThreshold * protocol.HeaderPower;

            List<float> decodeFrame = new List<float>(protocol.FullFrameSampleCount);

            DemodulateState state = DemodulateState.Sync;
            int[] dataBytes = null;
            int dataByteReceived = 0;
            int decodeOffset = 0;

            while (true)
            {
                if (!await TaskUtilities.WaitUntilUnless(() => !RxFIFO.IsEmpty, () => modemState != ModemState.Running))
                {
                    return;
                }

                float sample = RxFIFO.Pop();

                syncBuffer.ReadAndRemoveNext();
                syncBuffer.Add(sample);

                switch (state)
                {
                    case DemodulateState.Sync:
                        float syncPower = 0f;
                        float magnitude = 0f;
                        for (int j = 0; j < protocol.Header.Length; j++)
                        {
                            magnitude = MathF.Max(magnitude, MathF.Abs(syncBuffer[j]));
                            syncPower += syncBuffer[j] * protocol.Header[j];
                        }
                        if (magnitude > 0.01f)
                        {
                            syncPower *= protocol.HeaderMagnitude / magnitude;
                        }
                        if (writer != null)
                        {
                            writer.WriteSample(syncPower / minSyncPower);
                        }
                        if (syncPower > minSyncPower && syncPower > syncPowerLocalMax)
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
                                state = DemodulateState.DecodeLength;
                                decodeOffset = 0;
                                decode = false;
                            }
                        }
                        break;

                    case DemodulateState.DecodeLength:
                        if (writer != null)
                        {
                            writer.WriteSample(0f);
                        }
                        decodeFrame.Add(sample);
                        if (decodeFrame.Count >= protocol.SamplesPerByte * (protocol.LengthRedundancyBytes + 1))
                        {
                            DemodulateBytes(decodeFrame, ref decodeOffset, protocol.LengthRedundancyBytes + 1, lengthBufferRx);
                            if (!decoder.Decode(lengthBufferRx, protocol.LengthRedundancyBytes) || lengthBufferRx[0] == 0)
                            {
                                //ECC fail, quit
                                onFrameReceived.Invoke(null);
                                state = DemodulateState.Sync;
                                continue;
                            }

                            if (lengthBufferRx[0] == protocol.FrameMaxDataBytes)
                            {
                                dataBytes = fullFrameBufferRx;
                            }
                            else
                            {
                                dataBytes = new int[lengthBufferRx[0] + protocol.RedundancyBytes];
                            }
                            dataByteReceived = 0;
                            state = DemodulateState.DecodeData;
                        }
                        break;

                    case DemodulateState.DecodeData:
                        if (writer != null)
                        {
                            writer.WriteSample(0f);
                        }
                        decodeFrame.Add(sample);
                        if (decodeFrame.Count >= decodeOffset + protocol.SamplesPerByte)
                        {
                            dataBytes[dataByteReceived++] = DemodulateByte(decodeFrame, ref decodeOffset);
                            if (dataByteReceived == dataBytes.Length)
                            {
                                if (!decoder.Decode(dataBytes, protocol.RedundancyBytes))
                                {
                                    //ECC fail, quit
                                    onFrameReceived.Invoke(null);
                                    state = DemodulateState.Sync;
                                    continue;
                                }

                                byte[] result = new byte[dataBytes.Length - protocol.RedundancyBytes];
                                for (int i = 0; i < result.Length; i++)
                                {
                                    result[i] = (byte)dataBytes[i];
                                }
                                onFrameReceived.Invoke(result);
                                state = DemodulateState.Sync;
                            }
                        }
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
