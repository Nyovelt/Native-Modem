
#define THROW_PHASE_ERROR


using System;
using System.IO;
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

        ~IPProtocal()
        {
            modem.Dispose();
        }

        public void shell()
        {
            bool quit = false;
            // 寻找一个更好的交互式内建命令框架
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
