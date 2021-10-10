using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SampleStream : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }

        public float[] Samples { get; }

        int sampleCount = 0;

        public SampleStream(WaveFormat format, float[] samples)
        {
            WaveFormat = format;
            Samples = samples.Clone() as float[];
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int c = 0;
            int i = sampleCount;
            for (; i < Samples.Length && c < count; i++)
            {
                buffer[offset + c] = Samples[i];
                c++;
            }
            sampleCount += c;
            return c;
        }

        public float this[int i] => Samples[i];

        public int Length => Samples.Length;

        public void CopyTo(Array destination, int index)
        {
            Samples.CopyTo(destination, index);
        }
    }
}
