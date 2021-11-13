using B83.Collections;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Force.Crc32;

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
        readonly Queue<SendRequest> modulateQueue;
        readonly byte macAddress;
        readonly float[] buffer;

        ModemState modemState;

        public SynchronousModem(Protocol protocol, byte macAddress, string driverName, string saveTransportTo = null, string saveRecordTo = null)
        {
            this.protocol = protocol;
            this.macAddress = macAddress;

            TxFIFO = new SampleFIFO(protocol.SampleRate, protocol.SampleRate, saveTransportTo);
            RxFIFO = new SampleFIFO(protocol.SampleRate, protocol.SampleRate >> 1, saveRecordTo);
            modulateQueue = new Queue<SendRequest>();

            asioOut = new AsioOut(driverName);
            AsioUtilities.SetupAsioOut(asioOut);
            if (protocol.UseStereo)
            {
                asioOut.InitRecordAndPlayback(new MonoToStereoProvider16(TxFIFO.ToWaveProvider16()), 2, protocol.SampleRate);
            }
            else
            {
                asioOut.InitRecordAndPlayback(TxFIFO.ToWaveProvider(), 1, protocol.SampleRate);
            }

            buffer = new float[(protocol.UseStereo ? 2 : 1) * asioOut.FramesPerBuffer];
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
            if (protocol.UseStereo)
            {
                RxFIFO.PushStereo(buffer, sampleCount);
            }
            else
            {
                RxFIFO.Push(buffer, sampleCount);
            }
        }

        async Task PushIPG()
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

        async Task PushLowHigh(bool high)
        {
            if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.SamplesPerBit), () => modemState != ModemState.Running))
            {
                return;
            }

            float sample = high ? protocol.Amplitude : -protocol.Amplitude;
            for (int i = 0; i < protocol.SamplesPerBit; i++)
            {
                TxFIFO.Push(sample);
            }
        }

        async Task PushPreamble()
        {
            foreach (bool high in protocol.Preamble)
            {
                await PushLowHigh(high);
            }
        }

        async Task PushPhase(int phase)
        {
            if (!await TaskUtilities.WaitUntilUnless(() => TxFIFO.AvailableFor(protocol.SamplesPerBit), () => modemState != ModemState.Running))
            {
                return;
            }

            for (int i = 0; i < protocol.SamplesPerBit; i++)
            {
                TxFIFO.Push(protocol.PhaseLevel[phase]);
            }
        }

        async Task<int> ModulateByte(byte dataByte, int phase)
        {
            int levels = Protocol.Convert8To10(dataByte);
            for (int i = 0; i < 10; i++)
            {
                if (((levels >> i) & 0x01) == 0x01)
                {
                    phase = (phase + 1) & 0b11;
                }
                await PushPhase(phase);
            }
            return phase;
        }

        async Task<int> ModulateBytes(byte[] data, int phase)
        {
            foreach (byte dataByte in data)
            {
                phase = await ModulateByte(dataByte, phase);
            }
            return phase;
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
            await PushPreamble();
            await ModulateBytes(frame, protocol.StartPhase);
            await PushIPG();
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
                    await ModulateFrame(request.DestinationAddress, Protocol.FrameType.DATA, request.Data, byteCounter, protocol.FrameMaxDataBytes);
                    byteCounter += protocol.FrameMaxDataBytes;
                }

                int remainBytes = request.Data.Length - byteCounter;
                if (remainBytes != 0)
                {
                    await ModulateFrame(request.DestinationAddress, Protocol.FrameType.DATA, request.Data, byteCounter, (byte)remainBytes);
                }
            }
        }

        /// <summary>
        /// samples size should be: SamplesPerBit
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        bool DetectLowHigh(List<float> samples)
        {
            float sum = 0f;
            for (int i = 0; i < protocol.SamplesPerBit; i++)
            {
                sum += samples[i];
            }
            return sum > 0f;
        }

        //struct Temp
        //{
        //    public int Phase;
        //    public float Sum;
        //}
        //static RingBuffer<Temp> TEMP = new RingBuffer<Temp>(10);

        /// <summary>
        /// samples size should be: SamplesPerTenBits
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        byte DemodulateByte(List<float> samples, ref int phase, int time)
        {
            int count = 0;
            int raw = 0;
            for (int i = 0; i < 10; i++)
            {
                float sum = 0f;
                for (int j = 0; j < protocol.SamplesPerBit; j++)
                {
                    sum += samples[count++];
                }
                //TEMP.Add(new Temp() { Phase = phase, Sum = sum });
                try
                {
                    if (protocol.GetBit(sum, ref phase))
                    {
                        raw |= 0x1 << i;
                    }
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"{e.Message}, time: {(float)time / protocol.SampleRate}, bit: {i + 1} / 10");
                    //foreach (Temp t in TEMP)
                    //{
                    //    Console.Write($"{t.Sum} {t.Phase}|");
                    //}
                    //Console.WriteLine();
                }
            }
            return Protocol.Convert10To8(raw);
        }

        enum DemodulateState
        {
            Standby,
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
            int syncWaitSamples = protocol.SamplesPerBit - 1;
            int waitSFDTimeout = protocol.Preamble.Count;

            List<float> syncBitBuffer = new List<float>(protocol.SamplesPerBit);
            byte syncBits = 0;
            List<float> decodeByteBuffer = new List<float>(protocol.SamplesPerTenBits);
            byte[] headerBuffer = new byte[4];
            byte[] frameBuffer = null;
            int waitSFDCount = 0;

            DemodulateState state = DemodulateState.Sync;
            int bytesDecoded = 0;
            int phase = -1;

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
                    case DemodulateState.Standby:

                        break;

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
                                //Console.WriteLine($"Clock synced! sync power: {syncPowerLocalMax / minSyncPower * 100f}%, time: {(float)(time - syncWaitSamples) / protocol.WaveFormat.SampleRate}");
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
                            if (DetectLowHigh(syncBitBuffer))
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
                                phase = protocol.StartPhase;
                                //Console.WriteLine($"SFD detected. time: {(float)time / protocol.WaveFormat.SampleRate}");
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
                        if (decodeByteBuffer.Count == protocol.SamplesPerTenBits)
                        {
                            byte data = DemodulateByte(decodeByteBuffer, ref phase, time);
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
                                    
                                    if (headerBuffer[3] > protocol.FrameMaxDataBytes)
                                    {
                                        state = DemodulateState.Sync;
                                        Console.WriteLine($"Frame of invalid length ({headerBuffer[3]} received!)");
                                        break;
                                    }

                                    frameBuffer = new byte[8 + headerBuffer[3]];
                                    for (int i = 0; i < 4; i++)
                                    {
                                        frameBuffer[i] = headerBuffer[i];
                                    }
                                    //Console.WriteLine($"A frame with data length {headerBuffer[3]} detected!");
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
                                        onFrameReceived.Invoke(frameBuffer[0], frameBuffer[2], payload);
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
