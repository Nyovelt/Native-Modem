using B83.Collections;
using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Force.Crc32;

namespace Native_Modem
{
    public struct SendRequest
    {
        public byte DestinationAddress;
        public byte[] Data;

        public SendRequest(byte destinationAddress, byte[] data)
        {
            DestinationAddress = destinationAddress;
            Data = data;
        }
    }

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
        readonly Queue<SendRequest> modulateQueue;
        readonly byte macAddress;

        readonly float[] buffer = new float[1024];

        ModemState modemState;

        public SynchronousModem(Protocol protocol, byte macAddress, string driverName, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;

            TxFIFO = new SampleFIFO(protocol.WaveFormat, protocol.WaveFormat.SampleRate, saveTransportTo);
            RxFIFO = new SampleFIFO(protocol.WaveFormat, protocol.WaveFormat.SampleRate >> 1, saveRecordTo);
            modulateQueue = new Queue<SendRequest>();

            asioOut = new AsioOut(driverName);
            SetupAsioOut();
            asioOut.InitRecordAndPlayback(TxFIFO.ToWaveProvider(), protocol.WaveFormat.Channels, protocol.WaveFormat.SampleRate);

            modemState = ModemState.Idling;
        }

        /// <summary>
        /// The parameters of onFrameReceived are source address, frame type and payload
        /// </summary>
        /// <param name="onFrameReceived"></param>
        public void Start(Action<byte, byte, byte[]> onFrameReceived)
        {
            if (modemState != ModemState.Idling)
            {
                return;
            }

            modemState = ModemState.Running;

            _ = Modulate();
            _ = Demodulate(onFrameReceived);

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
        }

        public void Transport(SendRequest sendRequest)
        {
            modulateQueue.Enqueue(sendRequest);
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

        async Task ModulateIPG()
        {
            for (int i = 0; i < protocol.IPGBits; i++)
            {
                if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.SamplesPerBit), () => modemState != ModemState.Running))
                {
                    return;
                }

                for (int j = 0; j < protocol.SamplesPerBit; j++)
                {
                    TxFIFO.Push(0f);
                }
            }
        }

        async Task ModulateBit(bool bit)
        {
            if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.SamplesPerBit), () => modemState != ModemState.Running))
            {
                return;
            }

            float sample = bit ? protocol.Amplitude : -protocol.Amplitude;
            for (int i = 0; i < protocol.SamplesPerBit; i++)
            {
                TxFIFO.Push(sample);
            }
        }

        async Task ModulateBits(BitArray bits)
        {
            foreach (bool bit in bits)
            {
                await ModulateBit(bit);
            }
        }

        async Task ModulateByte(int dataByte)
        {
            for (int j = 0; j < 8; j++)
            {
                if (((dataByte >> j) & 0x1) == 1)
                {
                    await ModulateBit(true);
                }
                else
                {
                    await ModulateBit(false);
                }
            }
        }

        async Task ModulateBytes(byte[] data)
        {
            foreach (byte dataByte in data)
            {
                await ModulateByte(dataByte);
            }
        }

        async Task ModulateFrame(byte dest_addr, byte type, byte[] data, int offset, byte length)
        {
            byte[] frame = new byte[8 + length];
            frame[0] = dest_addr;
            frame[1] = macAddress;
            frame[2] = type;
            frame[3] = length;
            
            for (int i = 0; i < length; i++)
            {
                frame[i + 4] = data[i + offset];
            }

            Crc32Algorithm.ComputeAndWriteToEnd(frame);
            await ModulateBits(protocol.Preamble);
            await ModulateBytes(frame);
        }

        async Task Modulate()
        {
            if (modemState != ModemState.Running)
            {
                return;
            }

            while (true)
            {
                if (modulateQueue.Count == 0)
                {
                    if (!await TaskUtilities.WaitUntilUnless(() => modulateQueue.Count > 0, () => modemState != ModemState.Running))
                    {
                        return;
                    }
                }

                SendRequest request = modulateQueue.Dequeue();

                int fullFrames = request.Data.Length / protocol.FrameMaxDataBytes;
                int byteCounter = 0;
                for (int i = 0; i < fullFrames; i++)
                {
                    await ModulateFrame(request.DestinationAddress, (byte)Protocol.Type.DATA, request.Data, byteCounter, protocol.FrameMaxDataBytes);
                    byteCounter += protocol.FrameMaxDataBytes;
                    await ModulateIPG();
                }

                int remainBytes = request.Data.Length - byteCounter;
                if (remainBytes != 0)
                {
                    await ModulateFrame(request.DestinationAddress, (byte)Protocol.Type.DATA, request.Data, byteCounter, (byte)remainBytes);
                    await ModulateIPG();
                }
            }
        }

        bool DemodulateBit(List<float> samples)
        {
            float sum = 0f;
            for (int i = 0; i < protocol.SamplesPerBit; i++)
            {
                sum += samples[i];
            }
            return sum > 0f;
        }

        byte DemodulateByte(List<float> samples)
        {
            int count = 0;
            int ret = 0;
            for (int i = 0; i < 8; i++)
            {
                float sum = 0f;
                for (int j = 0; j < protocol.SamplesPerBit; j++)
                {
                    sum += samples[count++];
                }
                if (sum > 0f)
                {
                    ret |= 0x1 << i;
                }
            }
            return (byte)ret;
        }

        enum DemodulateState
        {
            Sync,
            Ready,
            Decode
        }

        async Task Demodulate(Action<byte, byte, byte[]> onFrameReceived)
        {
            if (modemState != ModemState.Running)
            {
                return;
            }

            bool sync = false;
            RingBuffer<float> syncBuffer = new RingBuffer<float>(protocol.ClockSync.Length);
            for (int i = 0; i < protocol.ClockSync.Length; i++)
            {
                syncBuffer.Add(0f);
            }
            float syncPowerLocalMax = 0f;
            float minSyncPower = 0.5f * protocol.ClockSyncPower;
            int syncWaitSamples = protocol.SamplesPerBit >> 1;
            int waitSFDTimeout = 32;

            List<float> syncBitBuffer = new List<float>(protocol.SamplesPerBit);
            byte syncBits = 0;
            List<float> decodeByteBuffer = new List<float>(protocol.SamplesPerByte);
            byte[] headerBuffer = new byte[4];
            byte[] frameBuffer = null;
            int waitSFDCount = 0;

            DemodulateState state = DemodulateState.Sync;
            int bytesDecoded = 0;

            int time = -1;
            while (true)
            {
                if (!await TaskUtilities.WaitUntilUnless(() => !RxFIFO.IsEmpty, () => modemState != ModemState.Running))
                {
                    return;
                }

                float sample = RxFIFO.Pop();
                time++;

                syncBuffer.ReadAndRemoveNext();
                syncBuffer.Add(sample);

                switch (state)
                {
                    case DemodulateState.Sync:
                        float syncPower = 0f;
                        for (int j = 0; j < protocol.ClockSync.Length; j++)
                        {
                            syncPower += syncBuffer[j] * protocol.ClockSync[j];
                        }

                        if (syncPower > minSyncPower && syncPower > syncPowerLocalMax)
                        {
                            syncPowerLocalMax = syncPower;
                            sync = true;
                            syncBitBuffer.Clear();
                        }
                        else if (sync)
                        {
                            syncBitBuffer.Add(sample);
                            if (syncBitBuffer.Count >= syncWaitSamples)
                            {
                                Console.WriteLine($"Clock synced! sync power: {syncPowerLocalMax / minSyncPower * 100f}%, time: {(float)(time - syncWaitSamples) / protocol.WaveFormat.SampleRate}");
                                syncPowerLocalMax = 0f;
                                state = DemodulateState.Ready;
                                syncBits = 0;
                                waitSFDCount = 0;
                                sync = false;
                            }
                        }
                        break;

                    case DemodulateState.Ready:
                        syncBitBuffer.Add(sample);
                        if (syncBitBuffer.Count == protocol.SamplesPerBit)
                        {
                            syncBits <<= 1;
                            if (DemodulateBit(syncBitBuffer))
                            {
                                syncBits |= 0x1;
                            }
                            syncBitBuffer.Clear();
                            waitSFDCount++;

                            if (syncBits == protocol.SFDByte)
                            {
                                state = DemodulateState.Decode;
                                bytesDecoded = 0;
                                decodeByteBuffer.Clear();
                                Console.WriteLine($"SFD detected. time: {(float)time / protocol.WaveFormat.SampleRate}");
                            }
                            else if (waitSFDCount >= waitSFDTimeout)
                            {
                                state = DemodulateState.Sync;
                                Console.WriteLine($"Clock synced but no SFD detected! sync byte: {syncBits}");
                            }
                        }
                        break;

                    case DemodulateState.Decode:
                        decodeByteBuffer.Add(sample);
                        if (decodeByteBuffer.Count == protocol.SamplesPerByte)
                        {
                            byte data = DemodulateByte(decodeByteBuffer);
                            decodeByteBuffer.Clear();
                            if (bytesDecoded < 4)
                            {
                                headerBuffer[bytesDecoded++] = data;
                                if (bytesDecoded == 4)
                                {
                                    if (headerBuffer[0] != macAddress)
                                    {
                                        state = DemodulateState.Sync;
                                        Console.WriteLine($"Frame to others ({headerBuffer[0]}) detected!");
                                        break;
                                    }

                                    frameBuffer = new byte[8 + headerBuffer[3]];
                                    for (int i = 0; i < 4; i++)
                                    {
                                        frameBuffer[i] = headerBuffer[i];
                                    }
                                    Console.WriteLine($"A frame with data length {headerBuffer[3]} detected!");
                                }
                            }
                            else
                            {
                                frameBuffer[bytesDecoded++] = data;
                                if (bytesDecoded == frameBuffer.Length)
                                {
                                    if (Crc32Algorithm.IsValidWithCrcAtEnd(frameBuffer))
                                    {
                                        byte[] payload = new byte[frameBuffer[3]];
                                        Array.Copy(frameBuffer, 4, payload, 0, payload.Length);
                                        onFrameReceived.Invoke(frameBuffer[0], frameBuffer[2], frameBuffer);
                                    }
                                    else
                                    {
                                        Console.WriteLine("A frame detected but failed CRC check!");
                                    }
                                    state = DemodulateState.Sync;
                                }
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
