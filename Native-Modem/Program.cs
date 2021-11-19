using NAudio.Wave.Asio;
using System;
using System.Collections.Generic;
using System.IO;

namespace Native_Modem
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            FullDuplexModem(false, true);
        }

        enum Operation
        {
            Help,
            MacPing,
            MacPerf,
            SendFile,
            CompareIO,
            Quit
        }

        static readonly Dictionary<Operation, string[]> ARGUMENTS = new Dictionary<Operation, string[]>(
            new KeyValuePair<Operation, string[]>[]
            {
                new KeyValuePair<Operation, string[]>(Operation.Help, Array.Empty<string>()),
                new KeyValuePair<Operation, string[]>(Operation.MacPing, new string[2] { "destination", "times" }),
                new KeyValuePair<Operation, string[]>(Operation.MacPerf, new string[1] { "destination" }),
                new KeyValuePair<Operation, string[]>(Operation.SendFile, new string[1] { "destination" }),
                new KeyValuePair<Operation, string[]>(Operation.CompareIO, Array.Empty<string>()),
                new KeyValuePair<Operation, string[]>(Operation.Quit, Array.Empty<string>())
            });

        static void FullDuplexModem(bool recordTx, bool recordRx)
        {
            Protocol protocol = Protocol.ReadFromFile("../../../protocol.conf");

            Console.WriteLine("Please enter your MAC address (in decimal): ");
            byte address;
            while (!byte.TryParse(Console.ReadLine(), out address))
            {
                Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            }

            FullDuplexModem modem = new FullDuplexModem(protocol, 
                address, 
                (source, data) =>
                {
                    FileStream outFile = new FileStream($"../../../OUTPUT.bin", FileMode.OpenOrCreate, FileAccess.Write);
                    outFile.SetLength(0);
                    BinaryWriter writer = new BinaryWriter(outFile);
                    writer.Write(data);
                    writer.Close();
                }, 
                info =>
                {
                    Console.Write($"\r{info}\n> ");
                },
                recordTx ? "../../../transportRecord.wav" : null,
                recordRx ? "../../../receiverRecord.wav" : null);

            Console.WriteLine("Modem started, please type in commands.");

            bool quit = false;
            while (!quit)
            {
                Console.Write("> ");
                string[] args = Console.ReadLine().Split(' ');
                if (args.Length == 0)
                {
                    continue;
                }

                if (!Enum.TryParse(args[0], true, out Operation op))
                {
                    Console.WriteLine("Invalid operation. Type 'help' to show help.");
                    continue;
                }

                if (args.Length != ARGUMENTS[op].Length + 1)
                {
                    Console.WriteLine("Argument count error. Type 'help' to show help.");
                    continue;
                }

                switch (op)
                {
                    case Operation.Help:
                        Console.WriteLine("Available operations:");
                        foreach (Operation o in Enum.GetValues<Operation>())
                        {
                            Console.Write($"\t{o.ToString().ToLower()}");
                            foreach (string arg in ARGUMENTS[o])
                            {
                                Console.Write($" {arg}");
                            }
                            Console.WriteLine();
                        }
                        break;

                    case Operation.MacPing:
                        if (!byte.TryParse(args[1], out byte pingDest) || !int.TryParse(args[2], out int pingTimes))
                        {
                            Console.WriteLine("Invalid arguments.");
                        }
                        else
                        {
                            for (int i = 0; i < pingTimes; i++)
                            {
                                modem.MacPing(pingDest, 500);
                            }
                        }
                        break;

                    case Operation.MacPerf:
                        Console.WriteLine("Not implemented!");
                        break;

                    case Operation.SendFile:
                        if (!byte.TryParse(args[1], out byte sendDest))
                        {
                            Console.WriteLine("Invalid destination.");
                        }
                        else
                        {
                            BinaryReader inputStream = new BinaryReader(new FileStream("../../../INPUT.bin", FileMode.Open, FileAccess.Read));
                            byte[] input = inputStream.ReadBytes((int)inputStream.BaseStream.Length);
                            inputStream.Close();
                            modem.TransportData((byte)sendDest, input);
                        }
                        break;

                    case Operation.CompareIO:
                        CompareResult();
                        break;

                    case Operation.Quit:
                        quit = true;
                        break;

                    default:
                        break;
                }
            }

            modem.Dispose();
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
    }
}
