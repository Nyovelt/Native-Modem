using System;

namespace Native_Modem
{
    public class SinusoidalSignal
    {
        readonly static float DEG_TO_RAD = MathF.PI / 180f;
        readonly static float TWO_PI = 2f * MathF.PI;

        public float Amplitude { get; private set; }
        public float Frequency { get; private set; }
        public float PhaseDegree { get; private set; }

        readonly float omega;
        readonly float phase;

        public SinusoidalSignal(float amplitude, float frequency, float phaseDegree = 0f)
        {
            Amplitude = amplitude;
            Frequency = frequency;
            PhaseDegree = phaseDegree;
            phase = phaseDegree * DEG_TO_RAD;
            omega = frequency * TWO_PI;
        }

        public float Evaluate(float time)
        {
            return Amplitude * MathF.Sin(omega * time + phase);
        }
    }
}
