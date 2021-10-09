using NAudio.Wave;

namespace Native_Modem
{
    public class Protocol
    {
        public SampleStream Header { get; }
        public SampleStream Carrier { get; }
        public WaveFormat WaveFormat { get; }
        public int SamplesPerBit { get; }
        public int FrameSize { get; }
        public int FrameSampleCount { get; }

        public Protocol(SampleStream header, SinusoidalSignal carrier, int sampleRate, int samplesPerBit, int frameSize)
        {
            Header = header;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            SamplesPerBit = samplesPerBit;
            FrameSize = frameSize;
            FrameSampleCount = samplesPerBit * frameSize;

            float[] carrierSamples = new float[FrameSampleCount];
            float time = 0f;
            float timeStep = 1f / sampleRate;
            for (int i = 0; i < FrameSampleCount; i++)
            {
                carrierSamples[i] = carrier.Evaluate(time);
                time += timeStep;
            }
            Carrier = new SampleStream(WaveFormat, carrierSamples);
        }
    }
}
