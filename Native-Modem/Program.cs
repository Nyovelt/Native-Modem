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
            string driverName = SelectAsioDriver();
            AsioDriver driver = AsioDriver.GetAsioDriverByName(driverName);
            driver.ControlPanel();
            Console.WriteLine("Press enter after setup the control panel");
            Console.ReadLine();
            driver.ReleaseComAsioDriver();

            Recorder recorder = new Recorder(driverName);

            WaveFormat signalFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            WaveFormat recordFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            recorder.SetupArgs(recordFormat);

            SignalGenerator signal = new SignalGenerator(new SinusoidalSignal[] {
                new SinusoidalSignal(1f, 1000f), new SinusoidalSignal(1f, 10000f) }, 
                signalFormat, 0.02f);
            if (!recorder.StartRecordAndPlayback(recordPath:"../../../record.wav", playbackProvider:signal))
            {
                Console.WriteLine("Start record and playback failed!");
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
            string[] asioDriverName = AsioDriver.GetAsioDriverNames();
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
