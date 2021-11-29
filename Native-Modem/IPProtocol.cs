
#define THROW_PHASE_ERROR


using NetTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using YamlDotNet.Serialization.NamingConventions;

namespace Native_Modem
{




    public class Interface
    {
        public string addresses { get; set; }
        public string gateway4 { get; set; }
    }



    public class IPProtocal
    {
        public Interface AthernetInterface;
        public FullDuplexModem modem;
        public IPProtocal()
        {
            getInterface(); // 获得 ip 配置
            startFullDuplexModem();
            shell();
        }

        enum Operation
        {
            UDPing
        }

        static readonly Dictionary<Operation, string[]> ARGUMENTS = new Dictionary<Operation, string[]>(
    new KeyValuePair<Operation, string[]>[]
    {
                new KeyValuePair<Operation, string[]>(Operation.UDPing, new string[1] { "destination" }),
    });

        ~IPProtocal()
        {
            modem.Dispose();
        }

        public void shell()
        {
            bool quit = false;
            // 寻找一个更好的交互式内建命令框架 // 失败
            while (!quit)
            {
                Console.Write(">");
                string[] args = Console.ReadLine().Split(' ');
                if (args.Length == 0)
                    continue;

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
                    case Operation.UDPing:
                        var pingDest = args[1];
                        if (!IsValidIP(pingDest))
                        {

                        }
                        else
                        {
                            UDPing(pingDest);
                        }
                        break;
                }
            }
        }

        public bool IsValidIP(string ip)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(ip, "[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}"))
            {
                string[] ips = ip.Split('.');
                if (ips.Length == 4 || ips.Length == 6)
                {
                    if (System.Int32.Parse(ips[0]) < 256 && System.Int32.Parse(ips[1]) < 256 & System.Int32.Parse(ips[2]) < 256 & System.Int32.Parse(ips[3]) < 256)
                        return true;
                    else
                    {
                        Console.WriteLine("IP invalid"); return false;
                    }
                }
                else
                {
                    Console.WriteLine("IP invalid"); return false;
                }

            }
            else
            {
                Console.WriteLine("IP invalid"); return false;
            }
        }

        public bool IsAthernetSubnet(string ipAddress)
        {
            if (!IsValidIP(ipAddress))
                return false;
            var rangeC = IPAddressRange.Parse(AthernetInterface.addresses);


            return rangeC.Contains(IPAddress.Parse(ipAddress)) ;
        }

        public void UDPing(string pingDest)  // pingDest 一定合法
        {
            if (IsAthernetSubnet(pingDest))
            {
                // 通过声卡发
            }
            else
            {
                // 通过网卡 (pcap) 发
            }
        }

        public void getInterface()
        {
            Console.Write("Please enter network config file: ");
            var configFile = Console.ReadLine();
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();
            Console.Write(File.ReadAllText(configFile));
            var myconfig = deserializer.Deserialize<Interface>(File.ReadAllText(configFile));
            AthernetInterface = myconfig; // problems here
        }

        public void startFullDuplexModem()
        {
            // 获得配置目录 
            Console.Write("Please enter work folder: ");
            string workFolder;
            while (true)
            {
                workFolder = Console.ReadLine();
                if (Directory.Exists(workFolder))
                {
                    break;
                }
                else
                {
                    Console.Write("Folder doesn't exist. Retry: ");
                }
            }
            Protocol protocol = Protocol.ReadFromFile(Path.Combine(workFolder, "protocol.conf"));
            //Console.WriteLine("Please enter your MAC address (in decimal): ");
            var rand = new Random();
            byte address = (byte)rand.Next(255);  // 1.广播，这样只要保证两个 mac 不一样就行了 2. 建立 mac - ip 表

            //while (!byte.TryParse(Console.ReadLine(), out address))
            //{
            //    Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            //}

            modem = new FullDuplexModem(protocol,
                address,
                (source, data) =>
                {
                    FileStream outFile = new FileStream(Path.Combine(workFolder, "OUTPUT.bin"), FileMode.OpenOrCreate, FileAccess.Write);
                    outFile.SetLength(0);
                    BinaryWriter writer = new BinaryWriter(outFile);
                    writer.Write(data);
                    writer.Close();
                },
                info =>
                {
                    Console.Write($"\r{info}\n> ");
                },
                protocol.RecordTx ? Path.Combine(workFolder, "transportRecord.wav") : null,
                protocol.RecordRx ? Path.Combine(workFolder, "receiverRecord.wav") : null);

            Console.WriteLine("Modem started, please type in commands.");
        }


    }
}
