using NAudio.Wave;
using System;
using STH1123.ReedSolomon;

namespace Native_Modem
{
    public class Protocol
    {
        public float[] Header { get; }
        public float HeaderPower { get; }
        public float HeaderMagnitude { get; }
        public float[] One { get; }
        public float[] Zero { get; }
        public WaveFormat WaveFormat { get; }
        public int SamplesPerBit { get; }
        public int SamplesPerByte { get; }
        public int FrameMaxDataBytes { get; }
        public int FullFrameSampleCount { get; }
        public float Threshold { get; }
        public GenericGF GaloisField { get; }
        public int RedundancyBytes { get; }
        public int LengthRedundancyBytes { get; }

        public Protocol(float[] header, SinusoidalSignal one, SinusoidalSignal zero, int sampleRate, int samplesPerBit, int maxFrameDataBytes, float threshold, GenericGF gf, int redundancyBytes, int lengthRedundancyBytes)
        {
            Header = header.Clone() as float[];
            HeaderPower = 0f;
            HeaderMagnitude = 0f;
            foreach (float sample in header)
            {
                HeaderPower += sample * sample;
                HeaderMagnitude = MathF.Max(HeaderMagnitude, MathF.Abs(sample));
            }
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            SamplesPerBit = samplesPerBit;
            SamplesPerByte = samplesPerBit << 3;
            FrameMaxDataBytes = maxFrameDataBytes;
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

            GaloisField = gf;
            RedundancyBytes = redundancyBytes;
            LengthRedundancyBytes = lengthRedundancyBytes;
            FullFrameSampleCount = Header.Length + SamplesPerByte * (FrameMaxDataBytes + RedundancyBytes);
        }
    }
}
