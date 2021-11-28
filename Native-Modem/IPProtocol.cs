
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
        readonly Interface AthernetInterface;
        readonly FullDuplexModem modem;
        public IPProtocal()
        {
            getInterface(); // 获得 ip 配置


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
        }


    }
}
