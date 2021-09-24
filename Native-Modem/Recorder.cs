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
        private IWaveProvider iWaveProvider;
        public Recorder(int DriverNameIndex =0 )
        {
            asioOut = new AsioOut(listAsioDricerNames(DriverNameIndex));
            Console.WriteLine("Choosing the Sound Card: {0}", asioDriverName[DriverNameIndex]);

        }


        public void setupRecordArgs(int inputChannelIndex = 0, int recordChannelCount = 1, int sampleRate = 44100 )
        {
            var inputChannels = asioOut.DriverInputChannelCount;
            asioOut.InputChannelOffset = inputChannelIndex;
            Console.WriteLine("We have {0} input recording Channels, and we are choosing the  Channel {1}, recordChannelCount {2}, sampleRate {3}", inputChannels, inputChannelIndex, recordChannelCount, sampleRate);
            asioOut.InitRecordAndPlayback(iWaveProvider, recordChannelCount, sampleRate);

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


    }
}
