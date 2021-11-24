using NAudio.Wave;
using System;
using System.Collections.Concurrent;

namespace Native_Modem
{
    public class SampleFIFO : ISampleProvider
    {
        readonly WaveFormat waveFormat;
        readonly ConcurrentQueue<float> sampleBuffer;
        readonly WaveFileWriter writer;

        public WaveFormat WaveFormat => waveFormat;
        public Action OnReadToEmpty;

        public SampleFIFO(int sampleRate, int size, string saveAudioTo = null)
        {
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            sampleBuffer = new ConcurrentQueue<float>();
            if (!string.IsNullOrEmpty(saveAudioTo))
            {
                writer = new WaveFileWriter(saveAudioTo, waveFormat);
            }
            else
            {
                writer = null;
            }
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Dispose();
            }
        }

        public bool IsEmpty => sampleBuffer.IsEmpty;

        public void Push(float sample)
        {
            sampleBuffer.Enqueue(sample);
            if (writer != null)
            {
                writer.WriteSample(sample);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samples = sampleBuffer.Count > count ? count : sampleBuffer.Count;
            int c = 0;
            for (; c < samples; c++)
            {
                sampleBuffer.TryDequeue(out float sample);
                buffer[offset + c] = sample;
            }
            for (; c < count; c++)
            {
                buffer[offset + c] = 0f;
            }

            if (samples > 0 && sampleBuffer.IsEmpty)
            {
                OnReadToEmpty?.Invoke();
            }

            return count;
        }

        public void Flush()
        {
            sampleBuffer.Clear();
        }
    }
}
