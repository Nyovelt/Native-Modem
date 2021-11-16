using System;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace Native_Modem
{
    public static class AsioUtilities
    {
        public static string SelectAsioDriver()
        {
            Console.WriteLine("Select an ASIO driver:");
            string[] asioDriverName = AsioDriver.GetAsioDriverNames();
            for (int i = 0; i < asioDriverName.Length; i++)
            {
                Console.WriteLine($"{i}: {asioDriverName[i]}");
            }
            string selected = asioDriverName[int.Parse(Console.ReadLine())];
            Console.WriteLine($"Choosing the ASIO driver: {selected}");
            return selected;
        }

        public static void SetupAsioOut(AsioOut asioOut)
        {
            Console.WriteLine("Select input channel:");
            var inputChannels = asioOut.DriverInputChannelCount;
            for (int i = 0; i < inputChannels; i++)
            {
                Console.WriteLine($"Input channel {i}: {asioOut.AsioInputChannelName(i)}");
            }
            int channel = int.Parse(Console.ReadLine());
            asioOut.InputChannelOffset = channel;
            Console.WriteLine($"Choosing the input channel: {asioOut.AsioInputChannelName(channel)}");

            var outputChannels = asioOut.DriverOutputChannelCount;
            Console.WriteLine("Select output channel:");
            for (int i = 0; i < outputChannels; i++)
            {
                Console.WriteLine($"Output channel {i}: {asioOut.AsioOutputChannelName(i)}");
            }
            int outChannel = int.Parse(Console.ReadLine());
            asioOut.ChannelOffset = outChannel;
            Console.WriteLine($"Choosing the output channel: {asioOut.AsioOutputChannelName(outChannel)}");
        }
    }
}
