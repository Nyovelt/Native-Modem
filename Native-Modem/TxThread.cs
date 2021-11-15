using System;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class FullDuplexModem
    {
        class TxThread
        {
            readonly Protocol protocol;

            public SampleFIFO TxFIFO { get; }
            
            public TxThread(Protocol protocol, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;

                TxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);
            }

            public void Dispose()
            {
                TxFIFO.Dispose();
            }

            async Task<bool> PushLevel(bool high, Func<bool> cancel)
            {
                if (!await TaskUtilities.WaitUntilUnless(
                    () => TxFIFO.AvailableFor(protocol.SamplesPerBit), 
                    () => cancel.Invoke()))
                {
                    return false;
                }

                float sample = high ? protocol.Amplitude : -protocol.Amplitude;
                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    TxFIFO.Push(sample);
                }
                return true;
            }

            async Task<bool> PushPreamble(Func<bool> cancel)
            {
                foreach (bool high in protocol.Preamble)
                {
                    if (!await PushLevel(high, cancel))
                    {
                        return false;
                    }
                }
                return true;
            }

            async Task<(bool, bool)> PushByte(byte data, bool phase, Func<bool> cancel)
            {
                int levels = Protocol.Convert8To10(data);
                for (int i = 0; i < 10; i++)
                {
                    if (((levels >> i) & 0x01) == 0x01)
                    {
                        phase = !phase;
                    }
                    if (!await PushLevel(phase, cancel))
                    {
                        return (false, phase);
                    }
                }
                return (true, false);
            }

            async Task<bool> PushBytes(byte[] data, Func<bool> cancel)
            {
                bool phase = protocol.StartPhase;
                foreach (byte dataByte in data)
                {
                    (bool notCanceled, bool newPhase) = await PushByte(dataByte, phase, cancel);
                    if (!notCanceled)
                    {
                        return false;
                    }
                    phase = newPhase;
                }
                return true;
            }

            public async Task<bool> Push(byte[] frame, Func<bool> cancel)
            {
                if (!await PushPreamble(cancel))
                {
                    return false;
                }

                if (!await PushBytes(frame, cancel))
                {
                    return false;
                }

                return await TaskUtilities.WaitForUnless(TxFIFO.Count * 1000 / protocol.SampleRate, cancel);
            }
        }
    }
}
