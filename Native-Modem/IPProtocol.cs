
#define THROW_PHASE_ERROR


using NetTools;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using PacketDotNet.Utils;
using YamlDotNet.Serialization.NamingConventions;
using ProtocolType = System.Net.Sockets.ProtocolType;

namespace Native_Modem
{




    public class Interface
    {
        public string Addresses { get; set; }
        public string Gateway4 { get; set; }
        public string EthernetAdapter { get; set; }
    }



    public class IpProtocal
    {
        public FullDuplexModem Modem;
        public string Node;
        public LibPcapLiveDevice Device;
        public bool Printsocketison;
        public string PrintsocketisonArg;
        public string IP;
        public bool Natrecv;
        public IpProtocal()
        {

            GetInterface(); // 获得 ip 配置
            Device.Open();
            Device.OnPacketArrival += Device_OnPacketArrival;
            Device.StartCapture();
            //SendICMP("192.168.18.1");
            if (Node is "1" or "2")
                startFullDuplexModem();
            Shell();
        }

        private enum Operation
        {
            printsocket,
            sendsocket,
            natsend,
            natrecv,
            ping
        }

        static readonly Dictionary<Operation, string[]> Arguments = new Dictionary<Operation, string[]>(
    new KeyValuePair<Operation, string[]>[]
    {
                new KeyValuePair<Operation, string[]>(Operation.printsocket, new string[1] { "source address" }),
                new KeyValuePair<Operation, string[]>(Operation.sendsocket, new string[1] { "destination address" }),
                new KeyValuePair<Operation, string[]>(Operation.natsend, new string[2] { "file path","destination" }),
                new KeyValuePair<Operation, string[]>(Operation.ping, new string[1] { "destination" }),
    });

        ~IpProtocal()
        {
            Modem.Dispose();
            Device.Dispose();
            Device.Close();
        }

        public void Shell()
        {
            // 寻找一个更好的交互式内建命令框架 // 失败
            while (true)
            {
                Console.Write(">");
                var args = Console.ReadLine()?.Split(' ');
                if (args is { Length: 0 })
                    continue;

                if (!Enum.TryParse(args[0], true, out Operation op))
                {
                    Console.WriteLine("Invalid operation. Type 'help' to show help.");
                    continue;
                }

                if (args.Length != Arguments[op].Length + 1)
                {
                    Console.WriteLine("Argument count error. Type 'help' to show help.");
                    continue;
                }

                switch (op)
                {
                    case Operation.printsocket:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments.");
                            break;
                        }

                        if (Node == "1")
                        {
                            Console.WriteLine("Node 1 cannot do printsocket");
                            break;
                        }
                        var pingSource = args[1];
                        Printsocketison = true;
                        PrintsocketisonArg = pingSource;
                        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                        {
                            Printsocketison = false;
                            PrintsocketisonArg = null;
                        };
                        while (Printsocketison)
                        {
                        }

                        Console.WriteLine("Exit printsocket");

                        break;
                    case Operation.sendsocket:
                        if (Node == "1")
                        {
                            Console.WriteLine("Node 1 cannot do sendsocket");
                            break;
                        }
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments.");
                            break;
                        }

                        var pingDst = args[1];
                        for (int i = 0; i < 10; i++)
                        {
                            Sendsocket(pingDst);
                            System.Threading.Thread.Sleep(1000);
                        }

                        Console.WriteLine("Finish sendsocket");
                        break;
                    case Operation.natsend:
                        byte[] input;
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments.");
                            break;
                        }
                        if (File.Exists(args[1]))
                        {
                            BinaryReader inputStream = new BinaryReader(new FileStream(args[1], FileMode.Open, FileAccess.Read));
                            input = inputStream.ReadBytes((int)inputStream.BaseStream.Length);
                            inputStream.Close();
                        }
                        else
                        {
                            Console.Write("Folder doesn't exist. Retry: ");
                            break;
                        }

                        if (Node == "1")
                        {
                            var packet = ConstructUdp(input, args[2]);
                            Console.WriteLine("sendfile to NAT");
                            Modem.TransportData(2, packet.Bytes);
                        }

                        if (Node == "3")
                        {
                            Console.WriteLine("sendfile to NAT");
                            SendUdp(input, args[2]);
                        }
                        break;
                    case Operation.natrecv:
                        if (Node == "2")
                        {
                            Console.WriteLine("Node 2 cannot do natrecv");
                            break;
                        }
                        Natrecv = true;
                        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                        {
                            Natrecv = false;
                        };
                        while (Natrecv)
                        {
                        }
                        Console.WriteLine("Exit natrecv");
                        break;
                    case Operation.ping:
                        if (Node != "1")
                        {
                            Console.WriteLine("Do not support ping except Node 1");
                            break;
                        }

                        break;
                    default:
                        break;

                }
            }
        }

        public void Sendsocket(string destination)
        {
            var data = new byte[20];
            var rand = new Random();
            rand.NextBytes(data);
            //var packet = ConstructUdpPacket(data, destination);
            var joinedBytes = string.Join(", ", data.Select(b => b.ToString()));
            Console.WriteLine($"Sending Packets to {destination}, data: {joinedBytes}");
            SendUdp(data, destination);
            //PrintUdpPacket(packet);
            //Device.SendPacket(packet);
        }

        public UdpPacket ConstructUdpPacket(byte[] data, string destination)
        {
            var packet = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            Console.WriteLine(packet);
            ByteArraySegment byteArraySegment = new ByteArraySegment(data);
            var udpPacket = new UdpPacket(byteArraySegment, packet);
            Console.WriteLine(udpPacket);
            udpPacket.PayloadData = data;
            PrintUdpPacket(udpPacket);
            return udpPacket;
        }

        public void SendUdp(byte[] dgram, string  destination)
        {
            ////construct ethernet packet
            //var ethernet = new EthernetPacket(Device.MacAddress, PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
            ////construct local IPV4 packet
            //var ipv4 = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            ////construct UDP packet
            //var udp = new UdpPacket(12345, 54321);
            ////add data in
            //udp.PayloadData = dgram;
            //udp.UpdateCalculatedValues();
            //ipv4.PayloadPacket = udp;
            //ipv4.UpdateCalculatedValues();
            //ethernet.PayloadPacket = ipv4;
            //ethernet.UpdateCalculatedValues();
            //// Console.WriteLine(ethernet);
            //Device.SendPacket(ethernet);
            var server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Parse(IP), 12345));
            var endpoint = new IPEndPoint(IPAddress.Parse(destination), 54321);
            server.SendTo(dgram, endpoint);

        }

        public void SendICMP(string destination)
        {
            //construct ethernet packet
            var ethernet = new EthernetPacket(PhysicalAddress.Parse("112233445566"), PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
            //construct local IPV4 packet
            var ipv4 = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            //ethernet.PayloadPacket = ipv4;
            const string cmdString = "Hello CS120";
            var sendBuffer = Encoding.ASCII.GetBytes(cmdString);
            var udp = new UdpPacket(12345, 54321);
            udp.PayloadData = sendBuffer;
            var icmp = new IcmpV4Packet(new ByteArraySegment(sendBuffer));
            icmp.TypeCode = IcmpV4TypeCode.EchoRequest;
            icmp.Sequence = 1;
            icmp.UpdateCalculatedValues();
            //add data in
            //udp.PayloadData = dgram;
            ipv4.PayloadPacket = icmp;
            // Console.WriteLine(ethernet);
            ethernet.PayloadPacket = ipv4;
            ethernet.UpdateCalculatedValues();
            Device.SendPacket(ethernet);
        }

        public EthernetPacket ConstructUdp(byte[] dgram, string destination)
        {
            //construct ethernet packet
            var ethernet = new EthernetPacket(PhysicalAddress.Parse("112233445566"), PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
            //construct local IPV4 packet
            var ipv4 = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            ethernet.PayloadPacket = ipv4;
            //construct UDP packet
            var udp = new UdpPacket(12345, 54321);
            //add data in
            udp.PayloadData = dgram;
            ipv4.PayloadPacket = udp;
            // Console.WriteLine(ethernet);
            return ethernet;
        }

        public void OnPacketreceived(byte source, byte[] data)
        {
            if (Node == "1")
            {
                var packet = Packet.ParsePacket(LinkLayers.Ethernet, data);
                if (packet == null)
                {
                    Console.WriteLine("Parse packet error");
                    return;
                }
                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket == null)
                {
                    Console.WriteLine("Parse packet error");
                    return;
                }
                var udpPacket = packet.Extract<UdpPacket>();
                if (udpPacket != null)
                {
                    if (Natrecv)
                    {
                        Console.WriteLine(
                            $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {udpPacket.SourcePort}, DestinationPort: {udpPacket.DestinationPort}");
                        FileStream outFile = new FileStream("OUTPUT.bin", FileMode.OpenOrCreate, FileAccess.Write);
                        outFile.SetLength(0);
                        BinaryWriter writer = new BinaryWriter(outFile);
                        writer.Write(udpPacket.PayloadData);
                        writer.Close();
                        Console.WriteLine("Natrecv OK\n");
                        Natrecv = false;
                    }
                }
            }

            if (Node == "2")
            {
                Console.WriteLine("Forward to Node 3");
                Device.SendPacket(data);
            }
        }
        public void PrintUdpPacket(UdpPacket udpPacket)
        {
            var ipPacket = udpPacket.Extract<IPPacket>();
            if (ipPacket == null) return;
            var _udpPacket = udpPacket.Extract<UdpPacket>();
            if (_udpPacket != null)
            {
                if (Printsocketison && ipPacket.SourceAddress.ToString() == PrintsocketisonArg)
                {
                    var joinedBytes = string.Join(", ", _udpPacket.PayloadData.Select(b => b.ToString()));
                    Console.WriteLine(
                        $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {_udpPacket.SourcePort}, DestinationPort: {_udpPacket.DestinationPort}, Payload: {joinedBytes}");
                }
            }
        }

        public void Device_OnPacketArrival(object s, PacketCapture e)
        {

            var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);

            var ipPacket = packet.Extract<IPPacket>();

            if (ipPacket == null) { return; }
            var udpPacket = packet.Extract<UdpPacket>();
            if (udpPacket != null)
            {
                if (Printsocketison && ipPacket.SourceAddress.ToString() == PrintsocketisonArg && ipPacket.DestinationAddress.ToString() == IP)
                {
                    var joinedBytes = string.Join(", ", udpPacket.PayloadData.Select(b => b.ToString()));
                    Console.WriteLine(
                        $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {udpPacket.SourcePort}, DestinationPort: {udpPacket.DestinationPort}, Payload: {joinedBytes}");
                }

                if (Node == "2 " && ipPacket.SourceAddress.ToString() == "192.168.1.2")
                {
                    var ethernet = packet.Extract<EthernetPacket>();
                    if (ethernet != null)
                    {
                        Console.WriteLine("Forward to Node 1");
                        Modem.TransportData(1, ethernet.Bytes);
                    }
                }

                if (Node == "3" && Natrecv && ipPacket.SourceAddress.ToString() == "192.168.1.2")
                {
                    Console.WriteLine(
                        $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {udpPacket.SourcePort}, DestinationPort: {udpPacket.DestinationPort}");
                    FileStream outFile = new FileStream("OUTPUT.bin", FileMode.OpenOrCreate, FileAccess.Write);
                    outFile.SetLength(0);
                    BinaryWriter writer = new BinaryWriter(outFile);
                    writer.Write(udpPacket.PayloadData);
                    writer.Close();
                    Console.WriteLine("Natrecv OK\n");
                    Natrecv = false;
                }
            }
            var icmpPacket = packet.Extract<IcmpV4Packet>();
            if (icmpPacket != null)
            {
                if (Node == "2")
                    Modem.TransportData(1, packet.Bytes);
            }
        }



        public void GetInterface()
        {
            Console.Write("Enter Node: ");
            Node = Console.ReadLine();
            Console.Write("Enter IP: ");
            IP = Console.ReadLine();
            var _devices = CaptureDeviceList.Instance;
            foreach (var dev in _devices)
                Console.WriteLine("{0}\n", dev.ToString());
            Console.WriteLine("Choose the Ethernet Adapter");
            var index = Int32.Parse(Console.ReadLine());
            Device = LibPcapLiveDeviceList.Instance[index];
            Console.WriteLine($"Choosing {Device.Name}");

            //string localIPAddress = "...";

            //var devices = CaptureDeviceList.Instance;

            //foreach (var dev in devices)
            //{
            //    // Console.Out.WriteLine("{0}", dev.Description);

            //    foreach (var addr in dev)
            //    {
            //        if (addr.Addr != null && addr.Addr.ipAddress != null)
            //        {
            //            Console.Out.WriteLine(addr.Addr.ipAddress);

            //            if (localIPAddress == addr.Addr.ipAddress.ToString())
            //            {
            //                Console.Out.WriteLine("Capture device found");
            //            }
            //        }
            //    }
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
            byte address = 0;
            switch (Node)
            {
                case "1":
                    address = 1;
                    break;
                case "2":
                    address = 2;
                    break;
            }

            //while (!byte.TryParse(Console.ReadLine(), out address))
            //{
            //    Console.WriteLine("MAC address invalid. It should be an integer in [0, 255]. Please try again: ");
            //}

            Modem = new FullDuplexModem(protocol,
                address,
                OnPacketreceived,
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
