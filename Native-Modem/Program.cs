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
            FullDuplexModemTest(false, true);
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

            Console.WriteLine($"Correct bytes: {sameCount} / {iLength}, {(float)sameCount / iLength * 100f}%");
        }

        static void FullDuplexModemTest(bool recordTx, bool recordRx)
        {
            Protocol protocol = new Protocol(
                amplitude: 0.02f,
                sampleRate: 48000,
                samplesPerBit: 2,
                maxPayloadSize: 128,
                ackTimeout: 250,
                maxRetransmit: 8);
            string driverName = AsioUtilities.SelectAsioDriver();
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

            FullDuplexModem modem = new FullDuplexModem(protocol, 
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
            Console.WriteLine("Press enter to stop modem...");

            //Get input bits and transport
            BinaryReader inputStream = new BinaryReader(new FileStream("../../../INPUT.bin", FileMode.Open, FileAccess.Read));
            byte[] input = inputStream.ReadBytes((int)inputStream.BaseStream.Length);
            inputStream.Close();
            modem.TransportData((byte)destination, input);
            for (int i = 0; i < 10; i++)
            {
                modem.MacPing((byte)destination, 200d);
            }

            Console.ReadLine();
            Console.WriteLine($"Received {frameCount} frames in total.");
            Console.WriteLine($"Received {byteCount} bytes in total.");

            writer.Close();
            modem.Dispose();
        }
    }
}
