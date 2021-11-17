using B83.Collections;
using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class SampleFIFO : ISampleProvider, IWaveProvider
    {
        readonly WaveFormat waveFormat;
        readonly RingBuffer<float> ringBuffer;
        readonly WaveFileWriter writer;

        public WaveFormat WaveFormat => waveFormat;
        public int Count => ringBuffer.Count;
        public Action OnReadToEmpty;

        public SampleFIFO(int sampleRate, int size, string saveAudioTo = null)
        {
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            ringBuffer = new RingBuffer<float>(size);
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

        public bool AvailableFor(int sampleCount)
        {
            return ringBuffer.Capacity - ringBuffer.Count >= sampleCount;
        }

        public bool IsEmpty => ringBuffer.Count == 0;

        public void Push(float sample)
        {
            ringBuffer.Add(sample);
            if (writer != null)
            {
                writer.WriteSample(sample);
            }
        }

        public float Pop()
        {
            return ringBuffer.ReadAndRemoveNext();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samples = ringBuffer.Count > count ? count : ringBuffer.Count;
            int c = 0;
            for (; c < samples; c++)
            {
                buffer[offset + c] = ringBuffer.ReadAndRemoveNext();
            }
            for (; c < count; c++)
            {
                buffer[offset + c] = 0f;
            }

            if (samples > 0 && ringBuffer.Count == 0)
            {
                OnReadToEmpty?.Invoke();
            }

            return count;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if ((count & 0x3) != 0)
            {
                throw new Exception("Read bytes can't divided by 4!");
            }
            int samples = Math.Min(ringBuffer.Count, count >> 2);
            int writePos = offset;
            for (int i = 0; i < samples; i++)
            {
                if (!BitConverter.TryWriteBytes(new Span<byte>(buffer, writePos, 4), ringBuffer.ReadAndRemoveNext()))
                {
                    throw new Exception("Write samples error!");
                }
                writePos += 4;
            }
            new Span<byte>(buffer, writePos, count - (samples << 2)).Fill(0);

            if (samples > 0 && ringBuffer.Count == 0)
            {
                OnReadToEmpty?.Invoke();
            }

            return count;
        }
    }
}
