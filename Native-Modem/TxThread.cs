using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        class TxThread
        {
            readonly Protocol protocol;

            public SampleFIFO TxFIFO { get; }

            bool stopped;
            
            public TxThread(Protocol protocol, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;

                TxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);

                stopped = false;
            }

            public void Dispose()
            {
                stopped = true;
                TxFIFO.Dispose();
            }

            async Task PushLevel(bool high)
            {
                if (!await TaskUtilities.WaitUntilUnless(
                    () => TxFIFO.AvailableFor(protocol.SamplesPerBit), 
                    () => stopped))
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
                    await PushLevel(high);
                }
            }

            async Task PushPhase(int phase)
            {
                if (!await TaskUtilities.WaitUntilUnless(
                    () => TxFIFO.AvailableFor(protocol.SamplesPerBit), 
                    () => stopped))
                {
                    return;
                }

                for (int i = 0; i < protocol.SamplesPerBit; i++)
                {
                    TxFIFO.Push(protocol.PhaseLevel[phase]);
                }
            }

            async Task<int> PushByte(byte data, int phase)
            {
                int levels = Protocol.Convert8To10(data);
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

            async Task<int> PushBytes(byte[] data, int phase)
            {
                foreach (byte dataByte in data)
                {
                    phase = await ModulateByte(dataByte, phase);
                }
                return phase;
            }

            public async Task Push(byte[] frame)
            {
                await PushPreamble();
                _ = await PushBytes(frame, protocol.StartPhase);
            }
        }
    }
}
