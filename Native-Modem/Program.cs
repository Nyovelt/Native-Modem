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
        //generated using MatLab, 480 samples, frequency range: 2kHz ~ 10kHz
        static readonly double[] FMCW = { 0,0.26093,0.50757,0.72091,0.88302,0.97865,0.99689,0.93264,0.78765,0.57118,0.29984,-0.0032862,-0.31027,-0.59079,-0.81515,-0.95753,-0.99931,-0.93184,-0.75848,-0.49525,-0.17005,0.17976,0.51134,0.78154,0.95267,0.99819,0.90739,0.68828,0.36796,-0.0098584,-0.39026,-0.71481,-0.93064,-0.99956,-0.90553,-0.65914,-0.29774,0.11911,0.51885,0.82826,0.98724,0.96124,0.74985,0.38925,-0.052555,-0.48762,-0.82518,-0.99094,-0.94435,-0.68986,-0.27886,0.19912,0.63492,0.92451,0.99448,0.82207,0.44401,-0.049273,-0.53375,-0.8825,-0.99946,-0.84739,-0.46159,0.055836,0.56125,0.90876,0.99301,0.7829,0.33515,-0.2184,-0.70788,-0.97727,-0.93537,-0.58903,-0.04599,0.51604,0.90646,0.98706,0.72318,0.20126,-0.3963,-0.85315,-0.99754,-0.76909,-0.24716,0.37305,0.85144,0.99607,0.74256,0.18622,-0.44989,-0.90225,-0.97657,-0.63408,-0.015335,0.61353,0.97366,0.89844,0.41433,-0.26305,-0.82145,-0.99546,-0.69539,-0.058023,0.61093,0.98126,0.86272,0.30714,-0.40834,-0.91594,-0.94649,-0.4761,0.25141,0.84622,0.98062,0.57298,-0.15708,-0.80161,-0.99079,-0.60833,0.13107,0.79634,0.98956,0.58725,-0.17437,-0.83193,-0.97514,-0.50663,0.28517,0.89748,0.93144,0.35673,-0.45575,-0.96761,-0.8301,-0.12781,0.66489,0.99987,0.63661,-0.1776,-0.86821,-0.93653,-0.32481,0.52725,0.99221,0.71787,-0.098426,-0.8427,-0.9429,-0.31339,0.56306,0.99868,0.64167,-0.23121,-0.91985,-0.85994,-0.091883,0.7549,0.97214,0.36898,-0.55032,-0.99974,-0.58459,0.34236,0.97058,0.73888,-0.15492,-0.91148,-0.84093,0.0010954,0.84447,0.9032,0.11367,-0.78562,-0.93768,-0.18837,0.74549,0.95366,0.22374,-0.72996,-0.95658,-0.22054,0.74109,0.94754,0.17868,-0.77742,-0.92325,-0.097336,0.83374,0.87624,-0.024096,-0.90036,-0.79568,0.18407,0.96214,0.66897,-0.3761,-0.99805,-0.48475,0.58548,0.98189,0.2376,-0.7863,-0.88557,0.065676,0.94032,0.68589,-0.39932,-0.99994,-0.37508,0.71404,0.91769,-0.026286,-0.93882,-0.66325,0.45867,0.99376,0.2461,-0.8227,-0.81705,0.26411,0.99715,0.40233,-0.73295,-0.88095,0.16681,0.98826,0.45282,-0.70943,-0.88659,0.17652,0.99262,0.40433,-0.76062,-0.83736,0.29251,0.99981,0.25034,-0.86712,-0.70711,0.5,0.96649,0.008763,-0.96064,-0.53004,0.66734,0.90273,-0.15384,-0.99019,-0.42526,0.73666,0.86766,-0.20663,-0.99413,-0.41632,0.72846,0.88404,-0.15167,-0.98271,-0.50474,0.63999,0.9418,0.013144,-0.93105,-0.67303,0.44695,0.99597,0.28412,-0.78358,-0.87091,0.12238,0.96303,0.623,-0.47417,-0.99635,-0.3217,0.73518,0.91942,0.021906,-0.8994,-0.77535,0.24185,0.98019,0.6031,-0.4538,-0.9999,-0.43219,0.6118,0.9823,0.28202,-0.72167,-0.94824,-0.16357,0.79235,0.91372,0.082062,-0.83253,-0.88961,-0.039424,0.84855,0.88199,0.03614,-0.84329,-0.89259,-0.072233,0.81579,0.91899,0.14734,-0.76133,-0.95432,-0.25988,0.67222,0.98689,0.40534,-0.5393,-0.99983,-0.57388,0.35468,0.97162,0.74767,-0.11585,-0.87835,-0.89892,-0.16897,0.69854,0.99003,0.47514,-0.42129,-0.97797,-0.75777,0.05693,0.82456,0.95333,0.35263,-0.51322,-0.99064,-0.72921,0.068955,0.81388,0.96705,0.42625,-0.41532,-0.96154,-0.83555,-0.13324,0.65749,0.99998,0.67059,-0.10279,-0.80553,-0.98105,-0.52353,0.26622,0.88455,0.94719,0.42328,-0.35775,-0.91855,-0.92575,-0.38319,0.38218,0.92198,0.92903,0.40734,-0.34133,-0.89651,-0.9553,-0.49334,0.23227,0.83071,0.9886,0.63068,-0.050367,-0.70244,-0.99769,-0.79502,-0.20234,0.4857,0.93691,0.94106,0.50379,-0.16465,-0.75418,-0.9995,-0.79701,-0.24398,0.41233,0.8876,0.98311,0.6657,0.074418,-0.5439,-0.93919,-0.95816,-0.60047,-0.012049,0.57746,0.94542,0.9591,0.62043,0.059117,-0.51979,-0.91193,-0.98487,-0.72015,-0.21412,0.3598,0.81197,0.99884,0.86657,0.46353,-0.079879,-0.5952,-0.92863,-0.98581,-0.75634,-0.31235,0.21626,0.68109,0.9569,0.97416,0.73443,0.3061,-0.19697,-0.6467,-0.93342,-0.99166,-0.81325,-0.44597,0.020811,0.47899,0.82703,0.99194,0.94326,0.69697,0.30923,-0.13758,-0.55306,-0.85713,-0.99482,-0.94507,-0.72243,-0.37203,0.040519,0.44204,0.76487,0.95785,0.99364,0.87145,0.61526,0.26833,-0.11476,-0.47706,-0.76768,-0.94859,-0.99889,-0.91637,-0.71634,-0.42824,-0.090793,0.25352,0.56397,0.80617,0.95562,0.99953,0.9373,0.77948,0.54574,0.26199,-0.042707,-0.33927,-0.60135,-0.80747,-0.94253,-0.9985,-0.9744,-0.87571,-0.71328,-0.5019,-0.25882 };
        static readonly double[] noise = new double[30];
        [STAThread]
        static void Main()
        {
            //GenerateRandomBits();
            //RecordAndPlay();
            //ModemTest();
            SynchronousModemTest();
            CompareResult();
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

                Console.WriteLine($"Correct bits: {sameCount} / {input.Length}, {(float)sameCount / input.Length}%");
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
            float[] header = new float[480];
            for (int i = 0; i < 480; i++)
            {
                header[i] = (float)FMCW[i] * 0.5f;
            }

            Protocol protocol = new Protocol(
                   header,
                   new SinusoidalSignal(0.5f, 1000f, 0f),
                   new SinusoidalSignal(0.5f, 1000f, 180f),
                   48000,
                   48,
                   96,
                   0f);
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
            SynchronousModem modem = new SynchronousModem(protocol, driverName);

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
            }, "../../../receiverRecord.wav");

            Console.WriteLine("Press enter to send a bit array");
            Console.ReadLine();

            //Get input bits and transport
            StreamReader inputStream = new StreamReader("../../../INPUT.txt");
            BitArray bitArray = BitReader.ReadBits(inputStream);
            inputStream.Close();
            modem.Transport(bitArray);

            Console.WriteLine("Press enter to stop modem...");
            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");

            modem.Stop();
            writer.Close();
            modem.Dispose();
        }

        static void ModemTest()
        {
            float[] header = new float[480];
            for (int i = 0; i < 480; i++)
            {
                //header[i] = 0.5f * MathF.Sin(i / 480f * 2f * MathF.PI * 5000f);
                header[i] = (float)FMCW[i] * 0.5f;
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
                        AudioFileReader reader = new AudioFileReader("../../../senderRecord.wav");
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
