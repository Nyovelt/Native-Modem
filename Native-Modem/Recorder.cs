using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Runtime.InteropServices;

namespace Native_Modem
{
    public class Recorder
    {
        private AsioOut asioOut;
        private string[] asioDriverName;
        private  WaveFileWriter writer;
        public Recorder(int DriverNameIndex =0 )
        {
            asioOut = new AsioOut(listAsioDricerNames(DriverNameIndex));
            Console.WriteLine("Choosing the Sound Card: {0}", asioDriverName[DriverNameIndex]);
            var samplesOutWav = @"..\..\..\testOutput";
            writer = new WaveFileWriter(samplesOutWav, new WaveFormat(16000, 24, 1));
        }

        ~Recorder()
        {
            asioOut.Dispose();
        }


        public void setupRecordArgs(int inputChannelIndex = 0, int recordChannelCount = 1, int sampleRate = 48000 )
        {
            var inputChannels = asioOut.DriverInputChannelCount;
            asioOut.InputChannelOffset = inputChannelIndex;
            Console.WriteLine("We have {0} input recording Channels, and we are choosing the  Channel {1}, recordChannelCount {2}, sampleRate {3}", inputChannels, inputChannelIndex, recordChannelCount, sampleRate);
            asioOut.InitRecordAndPlayback(null, recordChannelCount, sampleRate);
        }

        private string listAsioDricerNames(int DriverNameIndex = 0)
        {
            asioDriverName = AsioOut.GetDriverNames();
            foreach(var name in asioDriverName)
            {
                Console.WriteLine(name);
            }
            return asioDriverName[DriverNameIndex];
        }

        void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            var samples = e.GetAsInterleavedSamples();
            //foreach (var i in samples)
            //{
            //    Console.Write("{0} ", i);
            //}
            writer.WriteSamples(samples, 0, samples.Length);
        }

        public void startRecord()
        {
            asioOut.AudioAvailable += OnAsioOutAudioAvailable;
            asioOut.Play(); // start recording
        }

        public void stopRecord()
        {
            asioOut.Stop();
        }

    }
}
