using System;
using System.Collections.Generic;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class RxThread
        {
            enum RxState
            {
                WaitingForQuiet,
                WaitingForSync,
                Syncing,
                Demodulating
            }
            readonly Protocol protocol;

            public bool IsQuiet { get; private set; }
            public Action<byte[]> OnFrameReceived;

            RxState state;

            int quietCounter;

            //Frame detection
            readonly List<float> syncBitBuffer;
            uint syncBits;

            //Frame demodulation
            readonly List<float> byteSampleBuffer;
            readonly byte[] headerBuffer;
            byte[] frameBuffer;
            int bytesDecoded;
            bool phase;

            public RxThread(Protocol protocol)
            {
                this.protocol = protocol;

                IsQuiet = false;

                quietCounter = 0;

                syncBitBuffer = new List<float>(protocol.SamplesPerBit);

                byteSampleBuffer = new List<float>(protocol.SamplesPerTenBits);
                headerBuffer = new byte[4];

                state = RxState.WaitingForQuiet;
            }

            public void ProccessSamples(float[] buffer, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float sample = buffer[i];

                    if (MathF.Abs(sample) < protocol.Threshold)
                    {
                        if (!IsQuiet)
                        {
                            quietCounter++;
                            if (quietCounter >= protocol.QuietCriteria)
                            {
                                IsQuiet = true;
                            }
                        }
                    }
                    else
                    {
                        IsQuiet = false;
                        quietCounter = 0;
                    }

                    switch (state)
                    {
                        case RxState.WaitingForQuiet:
                            if (IsQuiet)
                            {
                                state = RxState.WaitingForSync;
                            }
                            break;

                        case RxState.WaitingForSync:
                            if (!IsQuiet)
                            {
                                state = RxState.Syncing;
                                syncBitBuffer.Clear();
                                syncBitBuffer.Add(sample);
                                syncBits = 0;
                            }
                            break;

                        case RxState.Syncing:
                            if (IsQuiet)
                            {
                                state = RxState.WaitingForSync;
                            }
                            else
                            {
                                ProcessSyncing(sample);
                            }
                            break;

                        case RxState.Demodulating:
                            if (IsQuiet)
                            {
                                state = RxState.WaitingForSync;
                            }
                            else
                            {
                                ProcessDemodulating(sample);
                            }
                            break;
                    }
                }
            }

            void ProcessSyncing(float sample)
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

                    if (syncBits == protocol.SFD)
                    {
                        state = RxState.Demodulating;
                        frameBuffer = null;
                        bytesDecoded = 0;
                        phase = protocol.StartPhase;
                        byteSampleBuffer.Clear();
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
                                Console.WriteLine("Invalid frame length!");
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

            bool DetectLevel(List<float> samples)
            {
                float sum = 0f;
                foreach (float sample in samples)
                {
                    sum += sample;
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

                    if (Protocol.GetBit(sum, ref phase))
                    {
                        raw |= 0x1 << i;
                    }
                }

                return Protocol.Convert10To8(raw);
            }
        }
    }
}
