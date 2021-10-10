using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SignalGenerator : ISampleProvider
    {

        readonly WaveFormat waveFormat;
        readonly SinusoidalSignal[] signals;
        readonly float gain;

        int sampleCount = 0;

        public SignalGenerator(SinusoidalSignal[] signals, WaveFormat waveFormat, float gain = 1f)
        {
            this.signals = signals;
            this.waveFormat = waveFormat;
            this.gain = gain;
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            float time = sampleCount / waveFormat.SampleRate;
            float step = 1f / waveFormat.SampleRate;
            for (int i = 0; i < count; i++)
            {
                float temp = 0f;
                foreach (SinusoidalSignal signal in signals)
                {
                    temp += signal.Evaluate(time);
                }

                buffer[offset + i] = temp * gain;

                time += step;
            }

            sampleCount += count;
            return count;
        }
    }
}
