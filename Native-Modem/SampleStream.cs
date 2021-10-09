using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SampleStream : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }

        readonly float[] samples;

        public SampleStream(WaveFormat format, float[] samples)
        {
            WaveFormat = format;
            this.samples = samples;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int c = 0;
            int i = offset;
            for (; i < samples.Length && c < count; i++)
            {
                buffer[c] = samples[i];
                c++;
            }
            return c;
        }

        public float this[int i] => samples[i];

        public int Length => samples.Length;
    }
}
