
#define THROW_PHASE_ERROR


using NetTools;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using PacketDotNet.Utils;
using ProtocolType = System.Net.Sockets.ProtocolType;
using System.Timers;
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
        public string NatrecvArg;
        public string Node3Ip;
        private pingstate pingstat;
        public Timer Timer;
        private DateTime dateTime;
        public IpProtocal()
        {

            GetInterface(); // 获得 ip 配置

            //var s = new Socket(AddressFamily.InterNetwork,
            //    SocketType.Raw, ProtocolType.Icmp);
            //s.Bind(new IPEndPoint(IPAddress.Parse(IP), 12345));

            //const string cmdString = "Hello CS120";
            //var sendBuffer = Encoding.ASCII.GetBytes(cmdString);
            //var headerBuffer = new byte[8];
            //var icmp = new IcmpV4Packet(new ByteArraySegment(headerBuffer));
            //icmp.TypeCode = IcmpV4TypeCode.EchoRequest;
            //icmp.Sequence = 1;
            //icmp.Id = 1;
            //icmp.PayloadData = sendBuffer;
            //icmp.Checksum = 0;
            //byte[] bytes = icmp.Bytes;
            //icmp.Checksum = (ushort)ChecksumUtils.OnesComplementSum(bytes, 0, bytes.Length);

            //s.SendTo(icmp.Bytes, new IPEndPoint(IPAddress.Parse("47.100.248.23"), 54321));
            //s.Close();
            //return;

            Device?.Open();
            if (Device != null)
                Device.OnPacketArrival += Device_OnPacketArrival;
            Device?.StartCapture();
            if (Node is "1" or "2")
                startFullDuplexModem();
            //for (var i = 0; i < 10; i++)
            //{ SendICMP("192.168.18.111"); System.Threading.Thread.Sleep(1000); }
            Shell();
            Modem?.Dispose();
        }

        private enum Operation
        {
            printsocket,
            sendsocket,
            natsend,
            natrecv,
            ping,
            quit
        }

        private enum pingstate
        {
            Waitforecho,
            Idle
        }

        static readonly Dictionary<Operation, string[]> Arguments = new(
    new KeyValuePair<Operation, string[]>[]
            {
                new(Operation.printsocket, new string[1] { "source address" }),
                new(Operation.sendsocket, new string[1] { "destination address" }),
                new(Operation.natsend, new string[2] { "file path","destination" }),
                new(Operation.natrecv, new string[1] { "source address" }),
                new(Operation.ping, new string[1] { "destination" }),
                new(Operation.quit, Array.Empty<string>())
            });

        public void Dispose()
        {
            Device?.Dispose();
            Device?.Close();
        }

        public void Shell()
        {
            bool quit = false;
            // 寻找一个更好的交互式内建命令框架 // 失败
            while (!quit)
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
                        if (args[1] != null)
                        {
                            NatrecvArg = args[1];
                        }
                        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                        {
                            Natrecv = false; NatrecvArg = null;
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


                        Natping(args[1]);


                        break;
                    case Operation.quit:
                        quit = true;
                        break;
                    default:
                        break;

                }
            }
        }

        private void Natping(string destination)
        {
            var data = ConstructICMP(destination,
                IcmpV4TypeCode.EchoRequest).Bytes;
            pingstat = pingstate.Waitforecho;
            Timer = new Timer();
            Timer.Interval = 10000d;
            Timer.AutoReset = false;
            Timer.Elapsed += (sender, e) =>
            {
                Console.WriteLine("ping timed out");
                pingstat = pingstate.Idle;
            };
            dateTime = DateTime.Now;
            Timer.Start();
            Console.WriteLine($"Ping {destination}");
            Modem.TransportData(2, data);
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

        public void SendUdp(byte[] dgram, string destination)
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
            server.Close();
        }

        public EthernetPacket ConstructICMP(string destination, IcmpV4TypeCode icmpV4TypeCode)
        {
            //construct ethernet packet
            var ethernet = new EthernetPacket(PhysicalAddress.Parse("112233445566"), PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
            //construct local IPV4 packet
            var ipv4 = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            ethernet.PayloadPacket = ipv4;
            const string cmdString = "Hello CS120";
            var sendBuffer = Encoding.ASCII.GetBytes(cmdString);
            var headerBuffer = new byte[8];

            var icmp = new IcmpV4Packet(new ByteArraySegment(headerBuffer));
            ipv4.PayloadPacket = icmp;
            icmp.TypeCode = icmpV4TypeCode;
            icmp.Checksum = 0;
            icmp.Sequence = 1;
            icmp.Id = 1;
            icmp.PayloadData = sendBuffer;
            byte[] bytes = icmp.Bytes;
            icmp.Checksum = (ushort)ChecksumUtils.OnesComplementSum(bytes, 0, bytes.Length);
            ipv4.UpdateCalculatedValues();
            ethernet.UpdateCalculatedValues();
            return ethernet;
        }

        public EthernetPacket ConstructUdp(byte[] dgram, string destination)
        {
            //construct ethernet packet
            var ethernet = new EthernetPacket(PhysicalAddress.Parse("112233445566"), PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
            //construct local IPV4 packet
            var ipv4 = new IPv4Packet(IPAddress.Parse(IP), IPAddress.Parse(destination));
            //construct UDP packet
            var udp = new UdpPacket(12345, 54321);
            //add data in
            udp.PayloadData = dgram;
            udp.UpdateCalculatedValues();
            ipv4.PayloadPacket = udp;
            ipv4.UpdateCalculatedValues();
            ethernet.PayloadPacket = ipv4;
            ethernet.UpdateCalculatedValues();
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
                    Console.WriteLine("Parse Ethrtnat packet error");
                    return;
                }

                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket == null)
                {
                    Console.WriteLine("Parse IP packet error");
                    return;
                }

                var udpPacket = packet.Extract<UdpPacket>();
                if (udpPacket != null)
                {
                    if (Natrecv)
                    {
                        var result = Encoding.UTF8.GetString(udpPacket.PayloadData);
                        Console.WriteLine(
                            $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {udpPacket.SourcePort}, DestinationPort: {udpPacket.DestinationPort}, PayloadData: {result}");
                        Console.WriteLine("Natrecv OK\n");
                        Natrecv = false;
                    }
                }

                var icmpPacket = packet.Extract<IcmpV4Packet>();
                if (icmpPacket != null)
                {
                    switch (icmpPacket.TypeCode)
                    {
                        case IcmpV4TypeCode.EchoReply:
                            if (pingstat == pingstate.Waitforecho)
                            {
                                Timer.Stop();
                                var timeslap = (DateTime.Now - dateTime).TotalMilliseconds;
                                var buffer = Encoding.UTF8.GetString(icmpPacket.PayloadData);
                                Console.WriteLine($"Source IP: {ipPacket.SourceAddress}, Payload: {buffer}, latency: {timeslap} ms ");
                                pingstat = pingstate.Idle;
                            }
                            break;
                        case IcmpV4TypeCode.EchoRequest:
                            var echoreply = ConstructICMP(
                                ipPacket.SourceAddress.ToString(),
                                IcmpV4TypeCode.EchoReply);


                            //construct ethernet packet
                            var ethernet = new EthernetPacket(PhysicalAddress.Parse("112233445566"), PhysicalAddress.Parse("665544332211"), EthernetType.IPv4);
                            //construct local IPV4 packet
                            var ipv4 = new IPv4Packet(IPAddress.Parse(IP), ipPacket.SourceAddress);
                            ethernet.PayloadPacket = ipv4;
                            //const string cmdString = "Hello CS120";
                            //var sendBuffer = Encoding.ASCII.GetBytes(cmdString);
                            var headerBuffer = new byte[8];

                            var icmp = new IcmpV4Packet(new ByteArraySegment(headerBuffer));
                            ipv4.PayloadPacket = icmp;
                            icmp.TypeCode = IcmpV4TypeCode.EchoReply;
                            icmp.Checksum = 0;
                            icmp.Sequence = icmpPacket.Sequence;
                            icmp.Id = icmpPacket.Id;
                            icmp.PayloadData = icmpPacket.PayloadData;
                            byte[] bytes = icmp.Bytes;
                            icmp.Checksum = (ushort)ChecksumUtils.OnesComplementSum(bytes, 0, bytes.Length);
                            ipv4.UpdateCalculatedValues();
                            ethernet.UpdateCalculatedValues();


                            Console.WriteLine($"Echo Reply to IP: {ipPacket.SourceAddress}");
                            Modem.TransportData(2, ethernet.Bytes);
                            break;
                    }
                }
            }

            if (Node == "2")
            {
                Console.WriteLine("Forwarding to Node 3");
                // Decode UDP
                var packet =
                    Packet.ParsePacket(LinkLayers.Ethernet, data); // get packet
                if (packet == null)
                {
                    Console.WriteLine("Parse Ethernat packet error");
                    return;
                }

                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket == null)
                {
                    Console.WriteLine("Parse IP packet error");
                    return;
                }

                var udpPacket = packet.Extract<UdpPacket>(); // Try if it is UDP
                if (udpPacket != null)
                {
                    // begin to send to Node 3 
                    var server = new Socket(AddressFamily.InterNetwork,
                        SocketType.Dgram, ProtocolType.Udp);
                    server.Bind(new IPEndPoint(IPAddress.Parse(IP), 12345));
                    var endpoint =
                        new IPEndPoint(ipPacket.DestinationAddress, 54321);
                    server.SendTo(udpPacket.PayloadData, endpoint);
                    server.Close();
                }

                var icmpPacket = packet.Extract<IcmpV4Packet>();
                if (icmpPacket is {TypeCode: IcmpV4TypeCode.EchoRequest or IcmpV4TypeCode.EchoReply})
                {
                    Console.WriteLine(icmpPacket.ToString());
                    var s = new Socket(AddressFamily.InterNetwork,
                        SocketType.Raw, ProtocolType.Icmp);
                    var a = new Random().Next(20000, 65536);
                    var b = new Random().Next(20000, 65536);
                    s.Bind(new IPEndPoint(IPAddress.Parse(IP),a
                        ));
                    Debug.Assert(icmpPacket != null, nameof(icmpPacket) + " != null");
                    s.SendTo(icmpPacket.Bytes,
                        new IPEndPoint(ipPacket.DestinationAddress,
                            b));
                    s.Close();
                }
                Console.WriteLine("Forward Success");
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

                if (Node == "2" && ipPacket.DestinationAddress.ToString() == IP && udpPacket.DestinationPort == 54321)
                {
                    var ethernet = packet.Extract<EthernetPacket>();
                    if (ethernet != null)
                    {
                        Console.WriteLine("Forward to Node 1");
                        Modem.TransportData(1, ethernet.Bytes);
                    }


                }

                if (Node == "3" && Natrecv && ipPacket.SourceAddress.ToString() == NatrecvArg && ipPacket.DestinationAddress.ToString() == IP)
                {
                    var result = System.Text.Encoding.UTF8.GetString(udpPacket.PayloadData);
                    Console.WriteLine(
                        $"SourceIP: {ipPacket.SourceAddress}, DestinationIP: {ipPacket.DestinationAddress}, SourcePort: {udpPacket.SourcePort}, DestinationPort: {udpPacket.DestinationPort}, PayloadData: {result}");
                    Console.WriteLine("Natrecv OK\n");

                }
            }
            var icmpPacket = packet.Extract<IcmpV4Packet>();
            if (icmpPacket != null)
            {
                if (Node == "2" && ipPacket.DestinationAddress.ToString()  == IP)
                {
                    if (icmpPacket.TypeCode == IcmpV4TypeCode.EchoReply)
                    {
                        Modem.TransportData(1, packet.Bytes);
                    }
                    if (icmpPacket.TypeCode == IcmpV4TypeCode.EchoRequest)
                    {
                        Modem.TransportData(1, packet.Bytes);
                    }
                }
            }
        }



        public void GetInterface()
        {
            Console.Write("Enter Node: ");
            Node = Console.ReadLine();
            if (Node == "1")
            {
                IP = "192.168.1.2";
            }
            else
            {
                Console.Write("Enter IP: ");
                IP = Console.ReadLine();

                var _devices = CaptureDeviceList.Instance;
                foreach (var dev in _devices)
                    Console.WriteLine("{0}\n", dev.ToString());
                Console.WriteLine("Choose the Ethernet Adapter");
                var index = Int32.Parse(Console.ReadLine());
                Device = LibPcapLiveDeviceList.Instance[index];
                Console.WriteLine($"Choosing {Device.Name}");

            }
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
