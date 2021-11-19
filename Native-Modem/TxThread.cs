using NAudio.Wave;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Timers;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class TxThread
        {
            readonly Protocol protocol;
            readonly RxThread Rx;
            readonly WasapiOut wasapiOut;

            public readonly SampleFIFO TxFIFO;
            readonly Queue<(byte[], Action<bool>)> pending;
            readonly Timer retryTimer;
            int tries;
            
            public TxThread(Protocol protocol, RxThread Rx, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;
                this.Rx = Rx;

                TxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);
                pending = new Queue<(byte[], Action<bool>)>();
                retryTimer = new Timer();
                retryTimer.AutoReset = false;
                retryTimer.Elapsed += (sender, e) =>
                {
                    TrySend();
                };
                tries = 0;

                wasapiOut = new WasapiOut(
                    device: WasapiUtilities.SelectOutputDevice(),
                    shareMode: protocol.SharedMode ? AudioClientShareMode.Shared : AudioClientShareMode.Exclusive,
                    true,
                    protocol.Delay);
                wasapiOut.Volume = 1f;
                wasapiOut.Init(TxFIFO);
                wasapiOut.Play();
            }

            public void Dispose()
            {
                wasapiOut.Stop();
                retryTimer.Stop();
                TxFIFO.Dispose();
                wasapiOut.Dispose();
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
                for (int i = 31; i >= 0; i--)
                {
                    PushLevel(((protocol.SFD >> i) & 0x1) == 0x1);
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
                if (TxFIFO.Count != 0)
                {
                    throw new Exception("TxFIFO not empty when writing frame!!!!!!!!!!!!!");
                }
                int sampleCount = frame.Length * protocol.SamplesPerTenBits + (protocol.SamplesPerBit << 5);
                if (!TxFIFO.AvailableFor(sampleCount))
                {
                    throw new Exception("Tx buffer overflow!");
                }

                PushPreamble();
                PushFrame(frame);
            }

            void OnFailed()
            {
                tries = 0;
                pending.Dequeue().Item2?.Invoke(false);

                if (pending.Count > 0)
                {
                    TrySend();
                }
            }

            void OnSendOver()
            {
                TxFIFO.OnReadToEmpty -= OnSendOver;
                tries = 0;
                pending.Dequeue().Item2?.Invoke(true);

                if (pending.Count > 0)
                {
                    TrySend();
                }
            }

            void TrySend()
            {
                tries++;
                if (Rx.IsQuiet)
                {
                    TxFIFO.OnReadToEmpty += OnSendOver;
                    PushFrameWithPreamble(pending.Peek().Item1);
                }
                else
                {
                    double backoff = protocol.GetBackoffTime(tries);
                    if (backoff < 0d)
                    {
                        OnFailed();
                    }
                    else if (backoff == 0d)
                    {
                        TrySend();
                    }
                    else
                    {
                        retryTimer.Interval = backoff;
                        retryTimer.Start();
                    }
                }
            }

            public void TransportFrame(byte[] frame, Action<bool> onSendComplete)
            {
                pending.Enqueue((frame, onSendComplete));
                if (pending.Count == 1)
                {
                    TrySend();
                }
            }
        }
    }
}
