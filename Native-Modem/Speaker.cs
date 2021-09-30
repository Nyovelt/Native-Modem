using NAudio.Wave;
using System;

namespace Native_Modem
{
    class Speaker
    {
        enum SpeakerState
        {
            Idling,
            Playing,
            Disposed
        }

        public AsioOut AsioOut { get; private set; }

        SpeakerState state = SpeakerState.Idling;
        AudioFileReader audiofilereader;

        public Speaker(string driverName)
        {
            AsioOut = new AsioOut(driverName);
        }

        public void Dispose()
        {
            if (state == SpeakerState.Playing)
            {
                StopPlayWAV();
            }

            AsioOut.Dispose();
            state = SpeakerState.Disposed;
        }

        public void SetupSpeakerArgs()
        {
            if (state == SpeakerState.Disposed)
            {
                return;
            }

            var outputChannels = AsioOut.DriverOutputChannelCount;
            Console.WriteLine("Select output channel:");
            for(int i = 0; i < outputChannels; i++)
            {
                Console.WriteLine($"Output channel {i}: {AsioOut.AsioOutputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            AsioOut.ChannelOffset = channel; // Todo: Different from the sample
            Console.WriteLine($"Choosing the input channel: {AsioOut.AsioOutputChannelName(channel)}");
        }

        public bool StartPlayWAV(string filePath)
        {
            if (state != SpeakerState.Idling)
            {
                return false;
            }

            audiofilereader = new AudioFileReader(filePath);
            AsioOut.Init(audiofilereader);
            AsioOut.Play();
            state = SpeakerState.Playing;
            return true;
        }

        public void StopPlayWAV()
        {
            if (state != SpeakerState.Playing)
            {
                return;
            }

            AsioOut.Stop();
            state = SpeakerState.Idling;
        }
    }
}
