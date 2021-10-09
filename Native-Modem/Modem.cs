using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public class Modem
    {
        readonly Protocol protocol;

        public Modem(Protocol protocol)
        {
            this.protocol = protocol;
        }

        SampleStream ModulateFrame(BitStream bitStream)
        {
            if (bitStream.Length != protocol.FrameSize)
            {
                throw new Exception("modulate frame bit stream length incorrect!");
            }

            float[] stream = new float[protocol.FrameSampleCount];
            int i = 0;
            foreach (int bit in bitStream)
            {
                for (int j = 0; j < protocol.SamplesPerBit; j++)
                {
                    int index = i * protocol.SamplesPerBit + j;
                    stream[index] = protocol.Carrier[index] * bit;
                }
                i++;
            }

            return new SampleStream(protocol.WaveFormat, stream);
        }

        BitStream DemodulateFrame(SampleStream sampleStream)
        {
            throw new NotImplementedException();
        }
    }
}
