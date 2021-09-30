using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class Recorder
    {
        enum RecorderState
        {
            Uninitialized,
            Idling,
            Recording,
            Disposed
        }

        public AsioOut AsioOut { get; private set; }

        WaveFileWriter writer;
        WaveFormat wavFormat;
        RecorderState state = RecorderState.Uninitialized;

        readonly float[] buffer = new float[1024];
        
        public Recorder(string driverName)
        {
            AsioOut = new AsioOut(driverName);
        }

        public void Dispose()
        {
            if (state == RecorderState.Recording)
            {
                StopRecordAndPlayback();
            }
            AsioOut.Dispose();
            state = RecorderState.Disposed;
        }

        public void SetupArgs(int recordChannelCount, int recordSampleRate)
        {
            if (state == RecorderState.Disposed)
            {
                return;
            }

            Console.WriteLine("Select input channel:");
            var inputChannels = AsioOut.DriverInputChannelCount;
            for (int i = 0; i < inputChannels; i++)
            {
                Console.WriteLine($"Input channel {i}: {AsioOut.AsioInputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            AsioOut.InputChannelOffset = channel;
            Console.WriteLine($"Choosing the input channel: {AsioOut.AsioInputChannelName(channel)}");

            var outputChannels = AsioOut.DriverOutputChannelCount;
            Console.WriteLine("Select output channel:");
            for (int i = 0; i < outputChannels; i++)
            {
                Console.WriteLine($"Output channel {i}: {AsioOut.AsioOutputChannelName(i)}");
            }
            int outChannel = int.Parse(Console.ReadLine());
            AsioOut.ChannelOffset = outChannel; // Todo: Different from the sample
            Console.WriteLine($"Choosing the input channel: {AsioOut.AsioOutputChannelName(outChannel)}");

            wavFormat = new WaveFormat(recordSampleRate, recordChannelCount);
            state = RecorderState.Idling;
        }

        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(buffer);
            writer.WriteSamples(buffer, 0, sampleCount);
        }

        public bool StartRecordAndPlayback(string recordPath, string playbackPath)
        {
            if (state != RecorderState.Idling)
            {
                return false;
            }

            AudioFileReader reader = new AudioFileReader(playbackPath);

            writer = new WaveFileWriter(recordPath, wavFormat);

            AsioOut.InitRecordAndPlayback(reader, wavFormat.Channels, wavFormat.SampleRate);
            AsioOut.AudioAvailable += OnAsioOutAudioAvailable;
            AsioOut.Play();
            state = RecorderState.Recording;
            return true;
        }

        public void StopRecordAndPlayback()
        {
            if (state != RecorderState.Recording)
            {
                return;
            }

            AsioOut.Stop();
            AsioOut.AudioAvailable -= OnAsioOutAudioAvailable;
            writer.Close();
            writer.Dispose();
            writer = null;
            state = RecorderState.Idling;
        }
    }
}
