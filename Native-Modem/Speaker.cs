using NAudio.Wave;
using System;

namespace Native_Modem
{
    class Speaker
    {
        private AsioOut asioOut;
        private string[] asioDriverName;
        private IWaveProvider myWaveProvider;
        private AudioFileReader audiofilereader;
        private bool disposed = false;
        private readonly float[] buffer = new float[1024];
        public Speaker()
        {
            Console.WriteLine("Configuring the Speaker");
            asioOut = new AsioOut(listAsioDriverNames());
            //string fileName = @"../../../b.wav";
            asioOut.ShowControlPanel();
            Console.WriteLine("Please configure the sound card, press enter to continue.");
            Console.ReadLine();
        }

        ~Speaker()
        {
            if (!disposed)
            {
                asioOut.Dispose();
            }
        }

        public void setupSpeakArgs(int speakChannelCount = 1)
        {
            var outputChannels = asioOut.DriverOutputChannelCount;
            Console.WriteLine("Select output channel:");
            Console.WriteLine($"speakChannelCount {speakChannelCount}");
            for(int i = 0; i < outputChannels; i++)
            {
                Console.WriteLine($"Output channel {i}: {asioOut.AsioOutputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            asioOut.ChannelOffset = channel; // Todo: Different from the sample
            Console.WriteLine($"Choosing the input channel: {asioOut.AsioInputChannelName(channel)}");
            //asioOut.Init(mySampleProvider);
        }

        private string listAsioDriverNames()
        {
            Console.WriteLine("Select a audio driver:");
            asioDriverName = AsioOut.GetDriverNames();
            for (int i = 0; i < asioDriverName.Length; i++)
            {
                Console.WriteLine($"{i}: {asioDriverName[i]}");
            }
            string selected = asioDriverName[int.Parse(Console.ReadLine())];
            Console.WriteLine($"Choosing the audio driver: {selected}");
            return selected;
        }

        public void startPlayWAV(string PATH)
        {
            audiofilereader = new AudioFileReader(PATH);
            asioOut.Init(audiofilereader);
            asioOut.Play();
        }

        public void stopPlayWAV()
        {
            asioOut.Stop();
                
        }
    }
}
