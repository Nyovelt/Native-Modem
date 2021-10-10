using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

namespace Native_Modem
{
    class Program 
    {
        static readonly float[] HEADER = { 0f, 0f, 0f, 0f };

        [STAThread]
        static void Main()
        {
            //GenerateRandomBits();
            //RecordAndPlay();
            ModemTest();
        }

        static void ModemTest()
        {
            float[] header = new float[480];
            for (int i = 0; i < 480; i++)
            {
                header[i] = 0.5f * MathF.Sin(i / 480f * 2f * MathF.PI * 5000f);
            }

            Protocol protocol = new Protocol(
                   header,
                   new SinusoidalSignal(0.5f, 1000f, 0f),
                   new SinusoidalSignal(0.5f, 1000f, 180f),
                   48000,
                   48,
                   96,
                   0f);
            Modem modem = new Modem(protocol);

            string driverName = SelectAsioDriver();
            Console.WriteLine("Do you want to configure the control panel? (y/n)");
            if (char.TryParse(Console.ReadLine(), out char c))
            {
                if (c == 'y')
                {
                    AsioDriver driver = AsioDriver.GetAsioDriverByName(driverName);
                    driver.ControlPanel();
                    Console.WriteLine("Press enter after setup the control panel");
                    Console.ReadLine();
                    driver.ReleaseComAsioDriver();
                }
            }
            Recorder recorder = new Recorder(driverName);
            recorder.SetupArgs(protocol.WaveFormat);

            Console.WriteLine("Sender or Receiver? (s/r)");
            if (char.TryParse(Console.ReadLine(), out char input))
            {
                switch (input)
                {
                    case 's':
                        //Get input bits
                        StreamReader inputStream = new StreamReader("../../../input.txt");
                        BitArray bitArray = BitReader.ReadBits(inputStream);
                        inputStream.Close();

                        //Modulate to samples
                        SampleStream stream = modem.Modulate(bitArray);

                        //Write to wav file
                        WaveFileWriter writer = new WaveFileWriter("../../../sender.wav", protocol.WaveFormat);
                        writer.WriteSamples(stream.Samples, 0, stream.Length);
                        writer.Dispose();

                        //Play and record
                        recorder.StartRecordAndPlayback(recordPath: "../../../senderRecord.wav", playbackProvider: stream);
                        Console.WriteLine("Press enter to stop sending...");
                        Console.ReadLine();
                        recorder.StopRecordAndPlayback();
                        break;
                    case 'r':
                        //Get input file (lossless transfer)
                        AudioFileReader reader = new AudioFileReader("../../../sender.wav");
                        float[] buffer = new float[960000];
                        int count = reader.Read(buffer, 0, 960000);
                        float[] samples = new float[count];
                        for (int i = 0; i < count; i++)
                        {
                            samples[i] = buffer[i];
                        }
                        reader.Close();

                        //Demodulate
                        int frames = 0;
                        foreach (BitArray result in modem.Demodulate(new SampleStream(protocol.WaveFormat, samples)))
                        {
                            for (int i = 0; i < result.Length; i++)
                            {
                                Console.Write(result[i] ? 1 : 0);
                            }
                            Console.WriteLine();
                            frames++;
                        }
                        Console.WriteLine(frames);
                        break;
                    default:
                        break;
                }
            }

            recorder.Dispose();
        }

        static void GenerateRandomBits()
        {
            StreamWriter file = new StreamWriter("../../../input.txt");
            Random random = new Random();
            for (int i = 0; i < 10000; i++)
            {
                file.Write(random.Next(0, 2));
            }
            file.Close();
        }

        static void RecordAndPlay()
        {
            string driverName = SelectAsioDriver();
            Console.WriteLine("Do you want to configure the control panel? (y/n)");
            if (char.TryParse(Console.ReadLine(), out char c))
            {
                if (c == 'y')
                {
                    AsioDriver driver = AsioDriver.GetAsioDriverByName(driverName);
                    driver.ControlPanel();
                    Console.WriteLine("Press enter after setup the control panel");
                    Console.ReadLine();
                    driver.ReleaseComAsioDriver();
                }
            }

            Recorder recorder = new Recorder(driverName);

            WaveFormat signalFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            WaveFormat recordFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            recorder.SetupArgs(recordFormat);

            SignalGenerator signal = new SignalGenerator(new SinusoidalSignal[] {
                new SinusoidalSignal(1f, 1000f), new SinusoidalSignal(1f, 10000f) },
                signalFormat, 0.02f);
            if (!recorder.StartRecordAndPlayback(recordPath: "../../../record.wav", playbackProvider: signal))
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
