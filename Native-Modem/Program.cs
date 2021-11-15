using NAudio.Wave.Asio;
using System;
using System.IO;

namespace Native_Modem
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            HalfDuplexModemTest(true, true);
            //SynchronousModemTest(false, false);
            CompareResult();
            Console.ReadLine();
        }

        static void CompareResult()
        {
            BinaryReader iStream = new BinaryReader(new FileStream("../../../INPUT.bin", FileMode.Open, FileAccess.Read));
            BinaryReader oStream = new BinaryReader(new FileStream("../../../OUTPUT.bin", FileMode.Open, FileAccess.Read));
            int iLength = (int)iStream.BaseStream.Length;
            int oLength = (int)oStream.BaseStream.Length;
            if (iLength != oLength)
            {
                Console.WriteLine($"Input and output have different length! In: {iLength}, Out: {oLength}");
                return;
            }
            byte[] input = iStream.ReadBytes(iLength);
            byte[] output = oStream.ReadBytes(oLength);
            iStream.Close();
            oStream.Close();

            int sameCount = 0;
            for (int i = 0; i < iLength; i++)
            {
                if (input[i] == output[i])
                {
                    sameCount++;
                }
            }

            Console.WriteLine($"Correct bits: {sameCount} / {iLength}, {(float)sameCount / iLength * 100f}%");
        }

        static void HalfDuplexModemTest(bool recordTx, bool recordRx)
        {
            Protocol protocol = new Protocol(
                amplitude: 0.05f,
                sampleRate: 48000,
                samplesPerBit: 2,
                maxPayloadSize: 128,
                useStereo: false,
                ackTimeout: 200);
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

            Console.WriteLine("Please enter your MAC address (in decimal): ");
            int address;
            while (!int.TryParse(Console.ReadLine(), out address) || address < 0 || address > 255)
            {
                Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            }

            FileStream outFile = new FileStream("../../../OUTPUT.bin", FileMode.OpenOrCreate, FileAccess.Write);
            outFile.SetLength(0);
            BinaryWriter writer = new BinaryWriter(outFile);
            int frameCount = 0;
            int byteCount = 0;

            HalfDuplexModem modem = new HalfDuplexModem(protocol, 
                (byte)address, 
                driverName,
                (source, data) =>
                {
                    Console.WriteLine($"Received frame {frameCount} from {source}");
                    writer.Write(data);
                    frameCount++;
                    byteCount += data.Length;
                }, 
                recordTx ? "../../../transportRecord.wav" : null,
                recordRx ? "../../../receiverRecord.wav" : null);

            Console.WriteLine("Modem started.");

            Console.WriteLine("Please enter destination MAC address (in decimal): ");
            int destination;
            while (!int.TryParse(Console.ReadLine(), out destination) || destination < 0 || destination > 255)
            {
                Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            }

            //Get input bits and transport
            BinaryReader inputStream = new BinaryReader(new FileStream("../../../INPUT.bin", FileMode.Open, FileAccess.Read));
            byte[] input = inputStream.ReadBytes((int)inputStream.BaseStream.Length);
            inputStream.Close();
            modem.TransportData((byte)destination, input);

            Console.WriteLine("Press enter to stop modem...");
            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");
            Console.WriteLine($"Received {byteCount} bytes in total.");

            writer.Close();
            modem.Dispose();
        }

        static void SynchronousModemTest(bool recordTx, bool recordRx)
        {
            Protocol protocol = new Protocol(
                amplitude: 0.05f,
                sampleRate: 48000,
                samplesPerBit: 2,
                maxPayloadSize: 64,
                useStereo: false,
                ackTimeout: 100);
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

            Console.WriteLine("Please enter your MAC address (in decimal): ");
            int address;
            while (!int.TryParse(Console.ReadLine(), out address) || address < 0 || address > 255)
            {
                Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            }

            SynchronousModem modem = new SynchronousModem(protocol, (byte)address, driverName, recordTx ? "../../../transportRecord.wav" : null, recordRx ? "../../../receiverRecord.wav" : null);

            //Start modem and prepare to write to file
            FileStream outFile = new FileStream("../../../OUTPUT.bin", FileMode.OpenOrCreate, FileAccess.Write);
            outFile.SetLength(0);
            BinaryWriter writer = new BinaryWriter(outFile);
            int frameCount = 0;
            int byteCount = 0;
            modem.Start((source, type, data) =>
            {
                Console.WriteLine($"Received frame {frameCount} from {source} of type {type}");
                writer.Write(data);
                frameCount++;
                byteCount += data.Length;
            });

            Console.WriteLine("Modem started.");

            Console.WriteLine("Please enter destination MAC address (in decimal): ");
            int destination;
            while (!int.TryParse(Console.ReadLine(), out destination) || destination < 0 || destination > 255)
            {
                Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            }

            //Get input bits and transport
            BinaryReader inputStream = new BinaryReader(new FileStream("../../../INPUT.bin", FileMode.Open, FileAccess.Read));
            byte[] input = inputStream.ReadBytes((int)inputStream.BaseStream.Length);
            inputStream.Close();
            modem.Transport(new SendRequest((byte)destination, input));

            Console.WriteLine("Press enter to stop modem...");
            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");
            Console.WriteLine($"Received {byteCount} bytes in total.");

            modem.Stop();
            writer.Close();
            modem.Dispose();
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
