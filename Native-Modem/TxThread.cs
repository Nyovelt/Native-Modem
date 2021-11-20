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
            readonly MMDevice outDevice;
            WasapiOut wasapiOut;

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

                outDevice = WasapiUtilities.SelectOutputDevice();
                wasapiOut = new WasapiOut(
                    device: outDevice,
                    shareMode: protocol.SharedMode ? AudioClientShareMode.Shared : AudioClientShareMode.Exclusive,
                    true,
                    protocol.Delay);
                wasapiOut.Volume = 1f;
                wasapiOut.Init(TxFIFO);
                wasapiOut.Play();
            }

            public void Restart()
            {
                wasapiOut.Stop();
                wasapiOut.Dispose();
                wasapiOut = new WasapiOut(
                    device: outDevice,
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

            void PushVolumeDown(int samples)
            {
                float magnitude = protocol.Amplitude;
                float step = magnitude / samples;
                for (int i = 0; i < samples; i++)
                {
                    TxFIFO.Push(i % 4 < 2 ? magnitude : -magnitude);
                    magnitude -= step;
                }
            }

            void PushVolumeUp(int samples)
            {
                float magnitude = 0f;
                float step = protocol.Amplitude / samples;
                for (int i = 0; i < samples; i++)
                {
                    TxFIFO.Push(i % 4 < 2 ? magnitude : -magnitude);
                    magnitude += step;
                }
            }

            void PushFrameWithPreamble(byte[] frame)
            {
                if (TxFIFO.Count != 0)
                {
                    throw new InvalidOperationException("TxFIFO not empty when writing frame!!!!!!!!!!!!!");
                }
                int sampleCount = frame.Length * protocol.SamplesPerTenBits + (protocol.SamplesPerBit << 5) + protocol.FadeoutSamples + protocol.FadeinSamples;
                if (!TxFIFO.AvailableFor(sampleCount))
                {
                    throw new OverflowException("Tx buffer overflow!");
                }

                PushVolumeUp(protocol.FadeinSamples);
                PushPreamble();
                PushFrame(frame);
                PushVolumeDown(protocol.FadeoutSamples);
            }

            void OnSendOver()
            {
                TxFIFO.OnReadToEmpty -= OnSendOver;
                Rx.OnCollisionDetected -= OnCollisionDetected;
                tries = 0;
                pending.Dequeue().Item2?.Invoke(true);

                if (pending.Count > 0)
                {
                    TrySend();
                }
            }

            void OnChannelQuiet()
            {
                Rx.OnQuiet -= OnChannelQuiet;
                TrySend();
            }

            void OnCollisionDetected()
            {
                Console.WriteLine("Collision detected!");
                TxFIFO.OnReadToEmpty -= OnSendOver;
                Rx.OnCollisionDetected -= OnCollisionDetected;
                TxFIFO.Flush();

                double backoff = protocol.GetBackoffTime(tries);
                if (backoff < 0d)
                {
                    tries = 0;
                    pending.Dequeue().Item2?.Invoke(false);

                    if (pending.Count > 0)
                    {
                        TrySend();
                    }
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

            void TrySend()
            {
                if (Rx.IsQuiet)
                {
                    tries++;
                    PushFrameWithPreamble(pending.Peek().Item1);
                    TxFIFO.OnReadToEmpty += OnSendOver;
                    Rx.OnCollisionDetected += OnCollisionDetected;
                }
                else
                {
                    Rx.OnQuiet += OnChannelQuiet;
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
