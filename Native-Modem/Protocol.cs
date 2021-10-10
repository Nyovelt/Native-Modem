using NAudio.Wave;

namespace Native_Modem
{
    public class Protocol
    {
        public float[] Header { get; }
        public SampleStream One { get; }
        public SampleStream Zero { get; }
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

            float[] samples = new float[samplesPerBit];
            float time = 0f;
            float timeStep = 1f / sampleRate;
            for (int i = 0; i < samplesPerBit; i++)
            {
                samples[i] = one.Evaluate(time);
                time += timeStep;
            }
            One = new SampleStream(WaveFormat, samples);

            time = 0f;
            for (int i = 0; i < samplesPerBit; i++)
            {
                samples[i] = zero.Evaluate(time);
                time += timeStep;
            }
            Zero = new SampleStream(WaveFormat, samples);
        }
    }
}
