using System;

namespace Native_Modem
{
    public class SinusoidalSignal
    {
        readonly static float TWO_PI = 2f * MathF.PI;

        public float Amplitude { get; private set; }
        public float Frequency { get; private set; }
        public float Phase { get; private set; }

        readonly float omega;

        public SinusoidalSignal(float amplitude, float frequency, float phase = 0f)
        {
            Amplitude = amplitude;
            Frequency = frequency;
            Phase = phase;

            omega = amplitude * TWO_PI;
        }

        public float Evaluate(float time)
        {
            return Amplitude * MathF.Sin(omega * time + Phase);
        }
    }
}
