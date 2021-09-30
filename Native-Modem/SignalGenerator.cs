using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SignalGenerator : ISampleProvider
    {
        readonly static float TWO_PI = 2f * MathF.PI;

        readonly WaveFormat waveFormat;
        readonly Signal[] signals;
        readonly float gain;

        public SignalGenerator(Signal[] signals, WaveFormat waveFormat, float gain = 1f)
        {
            this.signals = signals;
            this.waveFormat = waveFormat;
            this.gain = gain;
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            float time = offset / waveFormat.SampleRate;
            float step = 1f / waveFormat.SampleRate;
            for (int i = 0; i < count; i++)
            {
                float temp = 0f;
                foreach (Signal signal in signals)
                {
                    temp += signal.Amplitude * MathF.Sin(TWO_PI * signal.Frequency * time + signal.Phase);
                }

                buffer[i] = temp * gain;

                time += step;
            }

            return count;
        }
    }

    public struct Signal
    {
        public float Amplitude;
        public float Frequency;
        public float Phase;
    }
}
