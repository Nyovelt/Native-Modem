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
            readonly bool captureStereo;

            public bool IsQuiet { get; private set; }
            public Action<byte[]> OnFrameReceived;
            public Action OnCollisionDetected;
            public Action OnQuiet;

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

            public RxThread(Protocol protocol, bool captureStereo)
            {
                this.protocol = protocol;
                this.captureStereo = captureStereo;

                IsQuiet = false;

                quietCounter = 0;

                syncBitBuffer = new List<float>(protocol.SamplesPerBit);

                byteSampleBuffer = new List<float>(protocol.SamplesPerTenBits);
                headerBuffer = new byte[4];

                state = RxState.WaitingForQuiet;
            }

            public void ProcessData(byte[] data, int length)
            {
                if (captureStereo)
                {
                    for (int i = 0; i < length; i += 8)
                    {
                        float sum = BitConverter.ToSingle(data, i);
                        sum += BitConverter.ToSingle(data, i + 4);
                        ProcessSample(sum * 0.5f);
                    }
                }
                else
                {
                    for (int i = 0; i < length; i += 4)
                    {
                        ProcessSample(BitConverter.ToSingle(data, i));
                    }
                }
            }

            void ProcessSample(float sample)
            {
                float magnitude = MathF.Abs(sample);
                bool collision = false;
                if (magnitude < protocol.Threshold)
                {
                    if (!IsQuiet)
                    {
                        quietCounter++;
                        if (quietCounter >= protocol.QuietCriteria)
                        {
                            IsQuiet = true;
                            OnQuiet?.Invoke();
                        }
                    }
                }
                else
                {
                    IsQuiet = false;
                    quietCounter = 0;
                    if (magnitude > protocol.CollisionThreshold)
                    {
                        collision = true;
                        OnCollisionDetected?.Invoke();
                    }
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
                            if (collision)
                            {
                                state = RxState.WaitingForQuiet;
                            }
                            else
                            {
                                state = RxState.Syncing;
                                syncBitBuffer.Clear();
                                syncBitBuffer.Add(sample);
                                syncBits = 0;
                            }
                        }
                        break;

                    case RxState.Syncing:
                        if (IsQuiet)
                        {
                            state = RxState.WaitingForSync;
                        }
                        else if (collision)
                        {
                            state = RxState.WaitingForQuiet;
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
                        else if (collision)
                        {
                            state = RxState.WaitingForQuiet;
                        }
                        else
                        {
                            ProcessDemodulating(sample);
                        }
                        break;
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
                            byte length = Protocol.Frame.GetDataLength(headerBuffer);
                            if (length > protocol.FrameMaxDataBytes)
                            {
                                state = RxState.WaitingForQuiet;
                            }
                            else
                            {
                                frameBuffer = new byte[8 + length];
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
