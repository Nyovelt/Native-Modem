using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Runtime.InteropServices;

namespace Native_Modem
{
    class Program 
    {
        [STAThread]
        static void Main()
        {
            Recorder recorder = new Recorder(SelectAsioDriver());
            recorder.AsioOut.ShowControlPanel();
            Console.WriteLine("Press enter after setup the control panel");
            Console.ReadLine();

            WaveFormat recordFormat = new WaveFormat(48000, 16, 1);
            recorder.SetupArgs(recordFormat);

            if (!recorder.StartRecordAndPlayback(recordPath:"../../../record.wav"))
            {
                Console.WriteLine("Start record failed!");
                recorder.Dispose();
                return;
            }

            Console.WriteLine("Press enter to stop recording and playing...");
            Console.ReadLine();
            recorder.StopRecordAndPlayback();

            recorder.Dispose();
        }

        static string SelectAsioDriver()
        {
            Console.WriteLine("Select an ASIO driver:");
            string[] asioDriverName = AsioOut.GetDriverNames();
            for (int i = 0; i < asioDriverName.Length; i++)
            {
                Console.WriteLine($"{i}: {asioDriverName[i]}");
            }
            string selected = asioDriverName[int.Parse(Console.ReadLine())];
            Console.WriteLine($"Choosing the ASIO driver: {selected}");
            return selected;
        }
    }
}
