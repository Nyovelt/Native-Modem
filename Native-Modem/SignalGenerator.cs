using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SignalGenerator : IWaveProvider
    {
        readonly static float TWO_PI = 2f * MathF.PI;

        readonly WaveFormat waveFormat;
        readonly Signal[] signals;

        public SignalGenerator(Signal[] signals, WaveFormat waveFormat)
        {
            this.signals = signals;
            this.waveFormat = waveFormat;
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(byte[] buffer, int offset, int count)
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

                //TODO: write to buffer

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
