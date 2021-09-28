using NAudio.Wave;
using System;

namespace Native_Modem
{
    public class Recorder
    {
        private AsioOut asioOut;
        private string[] asioDriverName;
        private  WaveFileWriter writer;

        private bool disposed = false;
        private readonly float[] buffer = new float[1024];
        
        public Recorder()
        {
            Console.WriteLine("Configuring the Recorder");
            asioOut = new AsioOut(listAsioDriverNames());
            string fileName = @"../../../a.wav";
            writer = new WaveFileWriter(fileName, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
            asioOut.ShowControlPanel();
            Console.WriteLine("Please configure the sound card, press enter to continue.");
            Console.ReadLine();
        }

        ~Recorder()
        {
            if (!disposed)
            {
                writer.Dispose();
                asioOut.Dispose();
            }
        }


        public void setupRecordArgs(int recordChannelCount = 1, int sampleRate = 48000 )
        {
            Console.WriteLine($"recordChannelCount {recordChannelCount}, sampleRate {sampleRate}");
            Console.WriteLine("Select input channel:");
            var inputChannels = asioOut.DriverInputChannelCount;
            for (int i = 0; i < inputChannels; i++)
            {
                Console.WriteLine($"Input channel {i}: {asioOut.AsioInputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            asioOut.InputChannelOffset = channel;
            Console.WriteLine($"Choosing the input channel: {asioOut.AsioInputChannelName(channel)}");

            asioOut.InitRecordAndPlayback(null, recordChannelCount, sampleRate);
            Console.WriteLine("Press enter to start recording");
            Console.ReadLine();
        }

        private string listAsioDriverNames()
        {
            Console.WriteLine("Select a audio driver:");
            asioDriverName = AsioOut.GetDriverNames();
            for (int i = 0; i < asioDriverName.Length; i++)
            {
                Console.WriteLine($"{i}: {asioDriverName[i]}");
            }
            string selected =  asioDriverName[int.Parse(Console.ReadLine())];
            Console.WriteLine($"Choosing the audio driver: {selected}");
            return selected;
        }

        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int sampleCount = e.GetAsInterleavedSamples(buffer);

            writer.WriteSamples(buffer, 0, sampleCount);
             
        }

        public void startRecord()
        {
            asioOut.AudioAvailable += OnAsioOutAudioAvailable;
            asioOut.Play(); // start recording
            Console.WriteLine("Recording...Press enter to stop");
        }

        public void stopRecord()
        {
            asioOut.Stop();
            asioOut.AudioAvailable -= OnAsioOutAudioAvailable;
            writer.Dispose();
            asioOut.Dispose();
            disposed = true;
            Console.WriteLine("Record end. Saved to a.wav");
        }

    }
}
