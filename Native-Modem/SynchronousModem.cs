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
        readonly float[] preheater = new float[960];

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

            frameSampleCount = protocol.Header.Length + protocol.FrameSize * protocol.SamplesPerBit;
            TxFIFO = new SampleFIFO(protocol.WaveFormat, frameSampleCount << 3, saveTransportTo);
            RxFIFO = new SampleFIFO(protocol.WaveFormat, frameSampleCount << 1, saveRecordTo);
            modulateQueue = new Queue<BitArray>();

            asioOut = new AsioOut(driverName);
            SetupAsioOut();
            asioOut.InitRecordAndPlayback(TxFIFO.ToWaveProvider(), protocol.WaveFormat.Channels, protocol.WaveFormat.SampleRate);

            modemState = ModemState.Idling;
            
            if (!string.IsNullOrEmpty(saveSyncPowerTo))
            {
                writer = new WaveFileWriter(saveSyncPowerTo, protocol.WaveFormat);
            }
        }

        public void Start(FrameReceived onFrameReceived)
        {
            if (modemState != ModemState.Idling)
            {
                return;
            }

            modemState = ModemState.Running;

            _ = Modulate();
            //_ = Demodulate(onFrameReceived, 0.30f);

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
            //RxFIFO.Push(buffer, sampleCount);
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
        const int POWER_DISPLAY_INTERVAL = 2400;

        enum DemodulateState
        {
            Sync,
            Decode
        }

        public delegate void FrameReceived(BitArray bitArray);


        async Task Demodulate(FrameReceived onFrameReceived, float syncPowerThreshold)
        {
            bool decode = false;
            RingBuffer<float> syncBuffer = new RingBuffer<float>(protocol.Header.Length);
            for (int i = 0; i < protocol.Header.Length; i++)
            {
                syncBuffer.Add(0f);
            }
            float syncPowerLocalMax = 0f;
            float minSyncPower = syncPowerThreshold * protocol.HeaderPower;

            List<float> decodeFrame = new List<float>(protocol.FrameSize * protocol.SamplesPerBit);

            DemodulateState state = DemodulateState.Sync;

            int powerDisplayCounter = 0;
            float powerMax = 0f;
            float localMax = 0f;

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
                        powerMax = MathF.Max(powerMax, syncPower);
                        powerDisplayCounter++;
                        if (powerDisplayCounter >= POWER_DISPLAY_INTERVAL)
                        {
                            localMax = MathF.Max(localMax, powerMax);
                            Console.Write($"Current sync power: {powerMax / minSyncPower * 100f:000.000}%, Local max: {localMax / minSyncPower * 100f:000.000}%\r");
                            powerDisplayCounter = 0;
                            powerMax = 0f;
                        }
                        if (writer != null)
                        {
                            writer.WriteSample(syncPower / minSyncPower);
                        }
                        if (syncPower > minSyncPower && syncPower > syncPowerLocalMax)
                        {
                            //Console.WriteLine($"syncPower: {syncPower}, localMax: {syncPowerLocalMax}, threshold: {protocol.HeaderPower * syncPowerThreshold}");
                            syncPowerLocalMax = syncPower;
                            decode = true;
                            decodeFrame.Clear();
                        }
                        else if (decode)
                        {
                            decodeFrame.Add(sample);
                            if (decodeFrame.Count > protocol.Header.Length)
                            {
                                localMax = 0f;
                                Console.WriteLine($"Frame detected! sync power: {syncPowerLocalMax / minSyncPower * 100f}%                                 ");
                                syncPowerLocalMax = 0f;
                                state = DemodulateState.Decode;
                            }
                        }
                        break;

                    case DemodulateState.Decode:
                        if (writer != null)
                        {
                            writer.WriteSample(0f);
                        }
                        decodeFrame.Add(sample);
                        if (decodeFrame.Count == decodeFrame.Capacity)
                        {
                            bool[] bits = new bool[protocol.FrameSize];
                            int index = 0;
                            for (int j = 0; j < protocol.FrameSize; j++)
                            {
                                float sum = 0f;
                                for (int k = 0; k < protocol.SamplesPerBit; k++)
                                {
                                    sum += decodeFrame[index++] * protocol.One[k];
                                }
                                bits[j] = sum > protocol.Threshold;
                            }

                            // can add error correction / detection here

                            onFrameReceived(new BitArray(bits));

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
