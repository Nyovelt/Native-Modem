using B83.Collections;
using NAudio.Wave;

namespace Native_Modem
{
    public class SampleFIFO : ISampleProvider
    {
        readonly WaveFormat waveFormat;
        readonly RingBuffer<float> ringBuffer;
        readonly WaveFileWriter writer;

        public WaveFormat WaveFormat => waveFormat;

        public SampleFIFO(WaveFormat waveFormat, int size, string saveAudioTo = null)
        {
            this.waveFormat = waveFormat;
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

        public void Push(float[] sampleBuffer, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ringBuffer.Add(sampleBuffer[i]);
            }
            if (writer != null)
            {
                writer.WriteSamples(sampleBuffer, 0, count);
            }
        }

        public float Pop()
        {
            return ringBuffer.ReadAndRemoveNext();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int c = 0;
            for (; c < count; c++)
            {
                if (ringBuffer.Count > 0)
                {
                    buffer[offset + c] = ringBuffer.ReadAndRemoveNext();
                }
                else
                {
                    break;
                }
            }

            return c;
        }
    }
}
