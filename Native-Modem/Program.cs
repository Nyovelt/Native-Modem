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
        static bool Crr = true;
        static float[] preamble;
        [STAThread]
        static void Main()
        {
            //ReedSolomonTest();
            //return;
            //GenerateRandomBits();
            //RecordAndPlay();
            PreambleBuild(48000, 480, 1);
            //ModemTest(); // Remind: I have changed the PATH of sendRecord !!  
            SynchronousModemTest();
            CompareResult();
            Console.ReadLine();
        }

        static void ReedSolomonTest()
        {
            var input = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 };
            Console.WriteLine(BitConverter.ToString(input));
            GenericGF gf = new(285, 256, 1);
            ReedSolomonEncoder encoder = new(gf);
            var result = encoder.EncodeEx(input, 9);
            Console.WriteLine(BitConverter.ToString(result));

            ReedSolomonDecoder decoder = new(gf);
            decoder.TryDecodeEx(result, 9, out var decodeResult);
            Console.WriteLine(BitConverter.ToString(decodeResult));
        }

        static void PreambleBuild(int SampleRate, int SampleCount, float amplitude)
        {
            
            Debug.Assert(SampleRate != 0);
            preamble = new float[SampleCount];

            const float frequencyMin = 1000f;
            const float frequencyMax = 4000f;
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

            //if (input.Length != output.Length)
            if (false)
            {
                Console.WriteLine($"Input and output have different length! In: {input.Length}, Out: {output.Length}");
            }
            else
            {
                int sameCount = 0;
                for (int i = 0; i < 10000; i++)
                {
                    if (input[i] == output[i])
                    {
                        sameCount++;
                    }
                }

                Console.WriteLine($"Correct bits: {sameCount} / {input.Length}, {(float)sameCount / input.Length * 100f}%");
            }
        }

        static byte[] BitArrayToByteArray(BitArray bitArray)
        {
            byte[] result = new byte[(bitArray.Length - 1) >> 3 + 1];
            bitArray.CopyTo(result, 0);
            return result;
        }

        static void SynchronousModemTest()
        {
          

            // Headers
            Protocol protocol = new Protocol(
                   preamble,
                   new SinusoidalSignal(1f, 2500f, 0f),
                   new SinusoidalSignal(1f, 2500f, 180f),
                   48000,
                   32,
                   160,
                   0f);

            // Drivers
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

            //SynchronousModem modem = new SynchronousModem(protocol, driverName, null, "../../../receiverRecord.wav", "../../../syncPower.wav");
            SynchronousModem modem = new SynchronousModem(protocol, driverName, null, null, null);
            
            //Start modem and prepare to write to file
            StreamWriter writer = new StreamWriter("../../../OUTPUT.txt");
            int frameCount = 0;

            modem.Start(array =>
            {
                Console.Write($"Received frame {frameCount}:");

                foreach (bool bit in array)
                {
                    Console.Write(bit ? 1 : 0);
                    writer.Write(bit ? 1 : 0);
                }
                Console.WriteLine();
                frameCount++;
            });

            Console.WriteLine("Press enter to send a bit array");
            Console.ReadLine();

            //Get input bits and transport
            StreamReader inputStream = new StreamReader("../../../INPUT.txt");
            BitArray bitArray = BitReader.ReadBits(inputStream);
            inputStream.Close();
            if (Crr)
            {
                Encoder encoder = new Encoder();
                bitArray = encoder.encodeToIntArray(bitArray);
            }
            
            modem.Transport(bitArray);

            Console.WriteLine("Press enter to start another modem...");

            modem.Transport(bitArray);
            Console.WriteLine("Press enter to stop  modem...");
            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");

            modem.Stop();
            writer.Close();
            modem.Dispose();
            if (Crr)
            {
                StreamReader inputStream1 = new StreamReader("../../../OUTPUT.txt");
                BitArray bitArray1 = BitReader.ReadBits(inputStream1);
                inputStream1.Close();
                var Decoder = new Decoder();
                bitArray1 = Decoder.decodeToArray(bitArray1);
                StreamWriter writer1 = new StreamWriter("../../../OUTPUT.txt");
                foreach (bool bit in bitArray1)
                {
                    Console.Write(bit ? 1 : 0);
                    writer1.Write(bit ? 1 : 0);
                }
                writer1.Close();
            }
        }

        static void ModemTest()
        {
            float[] header = new float[480];
            for (int i = 0; i < 480; i++)
            {
                //header[i] = 0.5f * MathF.Sin(i / 480f * 2f * MathF.PI * 5000f);
                header[i] = (float)preamble[i] * 1f;
            }

            Protocol protocol = new Protocol(
                   header,
                   new SinusoidalSignal(1f, 8000f, 0f),
                   new SinusoidalSignal(1f, 8000f, 180f),
                   48000,
                   24,
                   100,
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
                        StreamReader inputStream = new StreamReader("../../../INPUT.txt");
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
                        //AudioFileReader reader = new AudioFileReader("../../../senderRecord.wav");
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
                        foreach (BitArray result in modem.Demodulate(new SampleStream(protocol.WaveFormat, samples), 3f))
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
