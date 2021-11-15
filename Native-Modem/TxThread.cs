using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class TxThread
        {
            readonly Protocol protocol;
            readonly RxThread Rx;

            public readonly SampleFIFO TxFIFO;
            readonly Queue<(byte[], Action)> pending;

            public bool Sending { get; private set; }
            
            public TxThread(Protocol protocol, RxThread Rx, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;
                this.Rx = Rx;

                TxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);
                pending = new Queue<(byte[], Action)>();

                Sending = false;

                Rx.OnQuiet += TrySend;
            }

            public void Dispose()
            {
                Rx.OnQuiet -= TrySend;

                TxFIFO.Dispose();
            }

            void PushLevel(bool high)
            {
                float sample = high ? protocol.Amplitude : -protocol.Amplitude;
                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    TxFIFO.Push(sample);
                }
            }

            void PushPreamble()
            {
                foreach (bool high in protocol.Preamble)
                {
                    PushLevel(high);
                }
            }

            void PushFrame(byte[] frame)
            {
                bool phase = protocol.StartPhase;
                foreach (byte dataByte in frame)
                {
                    int levels = Protocol.Convert8To10(dataByte);
                    for (int i = 0; i < 10; i++)
                    {
                        if (((levels >> i) & 0x01) == 0x01)
                        {
                            phase = !phase;
                        }
                        PushLevel(phase);
                    }
                }
            }

            void PushFrameWithPreamble(byte[] frame)
            {
                int sampleCount = frame.Length * protocol.SamplesPerTenBits + protocol.PreambleSampleCount;
                if (!TxFIFO.AvailableFor(sampleCount))
                {
                    throw new Exception("Tx buffer overflow!");
                }

                PushPreamble();
                PushFrame(frame);
            }

            void OnSendOver()
            {
                pending.Dequeue().Item2?.Invoke();
                Sending = false;
                TxFIFO.OnReadToEmpty -= OnSendOver;

                TrySend();
            }

            void TrySend()
            {
                if (!Sending && Rx.IsQuiet && pending.TryPeek(out (byte[], Action) next))
                {
                    Sending = true;
                    TxFIFO.OnReadToEmpty += OnSendOver;
                    PushFrameWithPreamble(next.Item1);
                }
            }

            public void TransportFrame(byte[] frame, Action onFrameSent)
            {
                pending.Enqueue((frame, onFrameSent));
                TrySend();
            }
        }
    }
}
