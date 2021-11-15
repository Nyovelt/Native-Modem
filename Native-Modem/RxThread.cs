using System;
using System.Collections.Generic;
using B83.Collections;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class RxThread
        {
            enum RxState
            {
                WaitingForQuiet,
                Syncing,
                Demodulating
            }
            readonly Protocol protocol;

            public SampleFIFO RxFIFO { get; }
            public bool IsQuiet { get; private set; }
            public Action<byte[]> OnFrameReceived;

            RxState state;

            int quietCounter;

            //Frame detection
            readonly int syncWaitSamples;
            readonly int waitSFDTimeout;
            readonly RingBuffer<float> syncBuffer;
            readonly List<float> syncBitBuffer;
            bool syncing;
            bool detected;
            float localMax;
            int waitSFDCount;
            byte syncBits;

            //Frame demodulation
            readonly List<float> byteSampleBuffer;
            readonly byte[] headerBuffer;
            byte[] frameBuffer;
            int bytesDecoded;
            bool phase;

            public RxThread(Protocol protocol, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;

                RxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);
                IsQuiet = false;

                quietCounter = 0;

                syncWaitSamples = protocol.SamplesPerBit - 1;
                waitSFDTimeout = protocol.Preamble.Count;
                syncBuffer = new RingBuffer<float>(protocol.ClockSync.Length);
                syncBitBuffer = new List<float>(protocol.SamplesPerBit);

                byteSampleBuffer = new List<float>(protocol.SamplesPerTenBits);
                headerBuffer = new byte[4];

                state = RxState.WaitingForQuiet;
            }

            public void Dispose()
            {
                RxFIFO.Dispose();
            }

            public void ProccessSamples(float[] buffer, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float sample = buffer[count];
                    if (MathF.Abs(sample) < protocol.Threshold)
                    {
                        if (!IsQuiet)
                        {
                            quietCounter++;
                            if (quietCounter >= protocol.QuietCriteria)
                            {
                                IsQuiet = true;
                                state = RxState.Syncing;
                            }
                        }
                    }
                    else if (IsQuiet)
                    {
                        IsQuiet = false;
                        quietCounter = 0;
                    }

                    switch (state)
                    {
                        case RxState.WaitingForQuiet:
                            if (IsQuiet)
                            {
                                state = RxState.Syncing;
                                OnEnterSyncing();
                            }
                            break;

                        case RxState.Syncing:
                            ProcessSyncing(sample);
                            break;

                        case RxState.Demodulating:
                            ProcessDemodulating(sample);
                            break;
                    }
                }
            }

            void ProcessSyncing(float sample)
            {
                syncBuffer.Add(sample);
                if (syncing)
                {
                    float syncPower = 0f;
                    for (int i = 0; i < protocol.ClockSync.Length; i++)
                    {
                        syncPower += syncBuffer[i] * protocol.ClockSync[i];
                    }

                    if (syncPower > protocol.ClockSyncPowerThreshold && syncPower > localMax)
                    {
                        localMax = syncPower;
                        detected = true;
                        syncBitBuffer.Clear();
                    }
                    else if (detected)
                    {
                        syncBitBuffer.Add(sample);
                        if (syncBitBuffer.Count >= syncWaitSamples)
                        {
                            localMax = 0f;
                            syncing = false;
                            syncBits = 0;
                            waitSFDCount = 0;
                            detected = false;
                        }
                    }
                }
                else
                {
                    syncBitBuffer.Add(sample);
                    if (syncBitBuffer.Count == protocol.SamplesPerBit)
                    {
                        syncBits <<= 1;
                        if (DetectLevel(syncBitBuffer))
                        {
                            syncBits |= 0x1;
                        }
                        syncBitBuffer.Clear();
                        waitSFDCount++;

                        if (syncBits == protocol.SFDByte)
                        {
                            state = RxState.Demodulating;
                            OnEnterDemodulating();
                        }
                        else if (waitSFDCount >= waitSFDTimeout)
                        {
                            state = RxState.WaitingForQuiet;
                        }
                    }
                }
            }

            void ProcessDemodulating(float sample)
            {
                byteSampleBuffer.Add(sample);
                if (byteSampleBuffer.Count == protocol.SamplesPerTenBits)
                {
                    byte data = DemodulateByte(byteSampleBuffer, ref phase);
                    byteSampleBuffer.Clear();
                    if (bytesDecoded < 4)
                    {
                        headerBuffer[bytesDecoded++] = data;
                        if (bytesDecoded == 4)
                        {
                            if (headerBuffer[3] > protocol.FrameMaxDataBytes)
                            {
                                state = RxState.WaitingForQuiet;
                            }
                            else
                            {
                                frameBuffer = new byte[8 + headerBuffer[3]];
                                Array.Copy(headerBuffer, frameBuffer, 4);
                            }
                        }
                    }
                    else
                    {
                        frameBuffer[bytesDecoded++] = data;
                        if (bytesDecoded == frameBuffer.Length)
                        {
                            OnFrameReceived.Invoke(frameBuffer);
                            state = RxState.WaitingForQuiet;
                        }
                    }
                }
            }

            void OnEnterSyncing()
            {
                syncing = true;
                detected = false;
                localMax = 0f;
                waitSFDCount = 0;
                syncBits = 0;
                syncBuffer.FillWith(0f);
            }

            void OnEnterDemodulating()
            {
                frameBuffer = null;
                bytesDecoded = 0;
                phase = protocol.StartPhase;
                byteSampleBuffer.Clear();
            }

            bool DetectLevel(List<float> samples)
            {
                float sum = 0f;
                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    sum += samples[i];
                }
                return sum > 0f;
            }
            
            byte DemodulateByte(List<float> samples, ref bool phase)
            {
                int sampleCount = 0;
                int raw = 0;
                for (int i = 0; i < 10; i++)
                {
                    float sum = 0f;
                    for (int j = 0; j < protocol.SamplesPerBit; j++)
                    {
                        sum += samples[sampleCount++];
                    }

                    if (protocol.GetBit(sum, ref phase))
                    {
                        raw |= 0x1 << i;
                    }
                }

                return Protocol.Convert10To8(raw);
            }
        }
    }
}
