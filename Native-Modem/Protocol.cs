using NAudio.Wave;

namespace Native_Modem
{
    public class Protocol
    {
        public float[] Header { get; }
        public float[] One { get; }
        public float[] Zero { get; }
        public WaveFormat WaveFormat { get; }
        public int SamplesPerBit { get; }
        public int FrameSize { get; }
        public float Threshold { get; }

        public Protocol(float[] header, SinusoidalSignal one, SinusoidalSignal zero, int sampleRate, int samplesPerBit, int frameSize, float threshold)
        {
            Header = header.Clone() as float[];
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            SamplesPerBit = samplesPerBit;
            FrameSize = frameSize;
            Threshold = threshold;

            One = new float[samplesPerBit];
            float time = 0f;
            float timeStep = 1f / sampleRate;
            for (int i = 0; i < samplesPerBit; i++)
            {
                One[i] = one.Evaluate(time);
                time += timeStep;
            }

            Zero = new float[samplesPerBit];
            time = 0f;
            for (int i = 0; i < samplesPerBit; i++)
            {
                Zero[i] = zero.Evaluate(time);
                time += timeStep;
            }
        }
    }
}
