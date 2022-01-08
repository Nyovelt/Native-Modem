
#define THROW_PHASE_ERROR


using NetTools;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections;
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
        public Timer Timer;
        private DateTime dateTime;
        public HashSet<int> TcpBindPort;
        public Queue<byte[]> savedData;
        public NaiveFtp naiveFtp;
        public bool flag;
        public IpProtocal()
        {

                GetInterface(); // 获得 ip 配置
            flag = false;
            TcpBindPort = new HashSet<int>();
            savedData = new Queue<byte[]>();
            if (Node is "1" or "2")
                startFullDuplexModem();


        }

        ~IpProtocal()
        {
            Modem?.Dispose();
        }


        public void Dispose()
        {
            Device?.Dispose();
            Device?.Close();
        }

        

        public void OnPacketreceived(byte source, byte[] data)
        {
            if (Node == "1")
            {
                savedData.Enqueue(data);
                flag = true;
            }

            if (Node == "2")
            {
                naiveFtp.Parse(Encoding.ASCII.GetString(data));
            }
        }


        public void GetInterface()
        {
            Console.Write("Enter Node: ");
            Node = Console.ReadLine();

            if (Node != "1") return;
            Console.Write("Enter IP: ");
            IP = Console.ReadLine();




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
