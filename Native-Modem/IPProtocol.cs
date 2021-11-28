
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


    }
}
