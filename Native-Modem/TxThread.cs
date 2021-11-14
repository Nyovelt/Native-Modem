using System;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class HalfDuplexModem
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

            async Task<bool> PushPhase(int phase, Func<bool> cancel)
            {
                if (!await TaskUtilities.WaitUntilUnless(
                    () => TxFIFO.AvailableFor(protocol.SamplesPerBit), 
                    () => cancel.Invoke()))
                {
                    return false;
                }

                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    TxFIFO.Push(protocol.PhaseLevel[phase]);
                }
                return true;
            }

            async Task<int> PushByte(byte data, int phase, Func<bool> cancel)
            {
                int levels = Protocol.Convert8To10(data);
                for (int i = 0; i < 10; i++)
                {
                    if (((levels >> i) & 0x01) == 0x01)
                    {
                        phase = (phase + 1) & 0b11;
                    }
                    if (!await PushPhase(phase, cancel))
                    {
                        return -1;
                    }
                }
                return phase;
            }

            async Task<int> PushBytes(byte[] data, int phase, Func<bool> cancel)
            {
                foreach (byte dataByte in data)
                {
                    phase = await PushByte(dataByte, phase, cancel);
                    if (phase == -1)
                    {
                        return -1;
                    }
                }
                return phase;
            }

            public async Task<bool> Push(byte[] frame, Func<bool> cancel)
            {
                if (!await PushPreamble(cancel))
                {
                    return false;
                }

                if (await PushBytes(frame, protocol.StartPhase, cancel) == -1)
                {
                    return false;
                }

                return await TaskUtilities.WaitForUnless(TxFIFO.Count * 1000 / protocol.SampleRate, cancel);
            }
        }
    }
}
