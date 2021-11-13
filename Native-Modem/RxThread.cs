using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using B83.Collections;

namespace Native_Modem
{
    public partial class HalfDuplexModem
    {
        class RxThread
        {
            readonly Protocol protocol;

            public SampleFIFO RxFIFO { get; }

            bool stopped;

            public RxThread(Protocol protocol, int bufferSize, string saveAudioTo = null)
            {
                this.protocol = protocol;

                RxFIFO = new SampleFIFO(protocol.SampleRate, bufferSize, saveAudioTo);

                stopped = false;
            }

            public void Dispose()
            {
                stopped = true;
                RxFIFO.Dispose();
            }

            public async Task WaitUntilQuiet()
            {
                int quietCount = 0;
                while (quietCount <= protocol.QuietCriteria)
                {
                    if (!await TaskUtilities.WaitUntilUnless(
                        () => !RxFIFO.IsEmpty, 
                        () => stopped))
                    {
                        return;
                    }

                    float sample = RxFIFO.Pop();

                    if (MathF.Abs(sample) < protocol.Threshold)
                    {
                        quietCount++;
                    }
                }
            }
        }
    }
}
