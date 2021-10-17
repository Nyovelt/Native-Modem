using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using STH1123.ReedSolomon;

namespace Native_Modem
{
    class Program 
    {
        static float[] preamble;
        [STAThread]
        static void Main()
        {
            //ReedSolomonTest();
            GenerateRandomBits();
            //RecordAndPlay();
            PreambleBuild(48000, 960, 1);
            SynchronousModemTest();
            CompareResult();
            Console.ReadLine();
        }

        static void ReedSolomonTest()
        {
            int[] input = new int[] { 16, 1234, 432154 };
            foreach (int i in input)
            {
                Console.Write($"{i}, ");
            }
            Console.WriteLine();
            GenericGF gf = new GenericGF(285, 256, 1);
            ReedSolomonEncoder encoder = new ReedSolomonEncoder(gf);
            encoder.Encode(input, 2);
            foreach (int i in input)
            {
                Console.Write($"{i}, ");
            }
            Console.WriteLine();
            input[1] = 97;
            foreach (int i in input)
            {
                Console.Write($"{i}, ");
            }
            Console.WriteLine();
            ReedSolomonDecoder decoder = new ReedSolomonDecoder(gf);
            Console.WriteLine(decoder.Decode(input, 2));
            foreach (int i in input)
            {
                Console.Write($"{i}, ");
            }
            Console.WriteLine();
        }

        static void PreambleBuild(int SampleRate, int SampleCount, float amplitude)
        {
            
            Debug.Assert(SampleRate != 0);
            preamble = new float[SampleCount];

            const float frequencyMin = 1000f;
            const float frequencyMax = 10000f;
            int half = SampleCount >> 1;
            float frequencyStep = (frequencyMax - frequencyMin) / SampleCount * 2f;
            float timeStep = 1f / SampleRate;
            float frequency = frequencyMin;
            float t = 0f;
            for (var i = 0; i < SampleCount; ++i)
            {
                if (i <= half)
                {
                    frequency += frequencyStep;
                }
                else
                {
                    frequency -= frequencyStep;
                }
                t += frequency * timeStep * 2f * MathF.PI;
                preamble[i] = MathF.Sin(t) * amplitude;
            }
        }

        static void CompareResult()
        {
            StreamReader iStream = new StreamReader("../../../INPUT.txt");
            StreamReader oStream = new StreamReader("../../../OUTPUT.txt");
            string input = iStream.ReadToEnd();
            string output = oStream.ReadToEnd();
            iStream.Close();
            oStream.Close();

            if (input.Length != output.Length)
            {
                Console.WriteLine($"Input and output have different length! In: {input.Length}, Out: {output.Length}");
            }
            else
            {
                int sameCount = 0;
                for (int i = 0; i < input.Length; i++)
                {
                    if (input[i] == output[i])
                    {
                        sameCount++;
                    }
                }

                Console.WriteLine($"Correct bits: {sameCount} / {input.Length}, {(float)sameCount / input.Length * 100f}%");
            }
        }

        static void SynchronousModemTest()
        {
            Protocol protocol = new Protocol(
                   preamble,
                   new SinusoidalSignal(1f, 4000f, 0f),
                   new SinusoidalSignal(1f, 4000f, 180f),
                   48000,
                   24,
                   11,
                   0f,
                   new GenericGF(285, 256, 1),
                   9,
                   2);
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
            //SynchronousModem modem = new SynchronousModem(protocol, driverName, "../../../transportRecord.wav", "../../../receiverRecord.wav", "../../../syncPower.wav");
            SynchronousModem modem = new SynchronousModem(protocol, driverName);

            //Start modem and prepare to write to file
            StreamWriter writer = new StreamWriter("../../../OUTPUT.txt");
            int frameCount = 0;
            modem.Start(array =>
            {
                Console.Write($"Received frame {frameCount}: ");
                if (array == null)
                {
                    Console.Write("Failed to receive!");
                }
                else
                {
                    foreach (byte byteData in array)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            int bit = (byteData >> i) & 0x1;
                            Console.Write(bit);
                            writer.Write(bit);
                        }
                    }
                }
                Console.WriteLine();
                frameCount++;
            });

            Console.WriteLine("Press enter to send a bit array");
            Console.ReadLine();

            //Get input bits and transport
            StreamReader inputStream = new StreamReader("../../../INPUT.txt");
            byte[] input = BitReader.ReadBitsIntoBytes(inputStream);
            inputStream.Close();
            modem.Transport(input);

            Console.WriteLine("Press enter to stop modem...");
            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");

            modem.Stop();
            writer.Close();
            modem.Dispose();
        }

        static void GenerateRandomBits()
        {
            StreamWriter file = new StreamWriter("../../../INPUT.txt");
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
