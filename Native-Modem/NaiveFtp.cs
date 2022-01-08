﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Native_Modem
{

    public class NaiveFtp
    {
        private IpProtocal _ipProtocal;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private TcpListener _tcpListener;
        private int _sendOffset;
        private int _recOffset;
        private int _pasvport = -1;
        private const string Hostname = "127.0.0.1";

        public NaiveFtp()
        {
            _ipProtocal = new IpProtocal();
            _ipProtocal.naiveFtp = this;


            if (_ipProtocal.Node == "1")
            {
                Initialize();
                Shell();
                _ipProtocal.Modem?.Dispose();
            }

            var flag = true;
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                flag = false;
            };
            while (flag) { }
        }

        ~NaiveFtp()
        {
            _tcpListener.Stop();
        }



        private void Initialize()
        {
            // Start TCP Listener
            var port = 19000;
            IPAddress localAddr = IPAddress.Parse(_ipProtocal.IP); //or 127.0.0.1
            _tcpListener = new TcpListener(localAddr, port);
            _tcpListener.Start();
            var t = new Thread(AthernetTunnel);
            t.Start();
            try
            {
                if (_ipProtocal.Node == "1")
                    _tcpClient = new TcpClient(_ipProtocal.IP, 19000); //or 127.0.0.1
                if (_ipProtocal.Node == "2")
                    _tcpClient = new TcpClient("127.0.0.1", 21); //or 127.0.0.1
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _stream = _tcpClient.GetStream();
            _sendOffset = 0;
            _recOffset = 0;

            var receiveData = new byte[256];

            // wait for a response
            var dataLength =
                _stream.Read(receiveData, _recOffset, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            _recOffset += dataLength;
        }

        private void Initialize2()
        {
            try
            {
                _tcpClient = new TcpClient("127.0.0.1", 21); //or 127.0.0.1
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _stream = _tcpClient.GetStream();
            _sendOffset = 0;
            _recOffset = 0;

            var receiveData = new byte[256];

            // wait for a response
            var dataLength =
                _stream.Read(receiveData, _recOffset, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());

            _ipProtocal.Modem.TransportData(1, Encoding.ASCII.GetBytes(recvdMessage));
            _recOffset += dataLength;
        }


        private void AthernetTunnel()
        {
            Console.WriteLine("AthernetTunnel Start \n");

            // Buffer for reading data
            var bytes = new byte[256];
            string data = null;

            while (true)
            {
                Console.Write("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                TcpClient client = _tcpListener.AcceptTcpClient();
                Console.WriteLine("Connected!");
                _ipProtocal.Modem.TransportData(2, Encoding.ASCII.GetBytes("Start"));

                data = null;

                // Get a stream object for reading and writing
                Console.WriteLine("1");
                NetworkStream stream = client.GetStream();
                Console.WriteLine("2");

                while (_ipProtocal.flag == false) { }

                while (_ipProtocal.savedData.TryDequeue(out var savedData))
                {
                    // Send back a response.
                    stream.Write(savedData, 0, savedData.Length);
                    Console.WriteLine("Sent: {0}", System.Text.Encoding.ASCII.GetString(savedData, 0, savedData.Length));
                }
                _ipProtocal.flag = true;
                int i;

                // Loop to receive all the data sent by the client.
                while (true)
                {
                    i = stream.Read(bytes, 0, bytes.Length);
                    // Translate data bytes to a ASCII string.
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Received: {0}", data);
                    _ipProtocal.Modem.TransportData(2, bytes);


                    // Process the data sent by the client.
                    while (_ipProtocal.savedData.TryDequeue(out var savedData))
                    {
                        // Send back a response.
                        stream.Write(savedData, 0, savedData.Length);
                        Console.WriteLine("Sent: {0}", System.Text.Encoding.ASCII.GetString(savedData, 0, savedData.Length));
                    }


                }
            }
        }

        private void Send(string message)
        {

            Flush();
            Console.WriteLine($"Sending {message}");
            var data = System.Text.Encoding.ASCII.GetBytes(message);
            _stream.Write(data, 0, data.Length);
            _sendOffset += data.Length;

            // wait for a response
            while (!_stream.DataAvailable)
            {
            }

            var receiveData = new byte[256];
            var dataLength = _stream.Read(receiveData, 0, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            var ret = recvdMessage.ToString();
            Console.WriteLine(ret);
          





            if (message == "PASV\r\n" && ret.Split(' ')[0] == "227")
            {
                ret = (ret.Split(' ')[4]).Replace("\r", "").Replace("\n", "")
                    .Replace("(", "").Replace(")", "").Replace(".", "");
                Console.WriteLine("PASV port changed to {0}",
                    int.Parse(ret.Split(',')[4]) * 256 +
                    int.Parse(ret.Split(',')[5]));
                _pasvport = int.Parse(ret.Split(',')[4]) * 256 +
                            int.Parse(ret.Split(',')[5]);

            }

        }


        private void Flush()
        {
            while (_stream.DataAvailable)
            {
                var receiveData = new byte[256];
                var dataLength = _stream.Read(receiveData, 0, receiveData.Length);
                var recvdMessage =
                    System.Text.Encoding.ASCII.GetString(receiveData, 0,
                        dataLength);
                var ret = recvdMessage.ToString();
                Console.WriteLine(ret);
                _ipProtocal.Modem.TransportData(1, receiveData);
            }
        }

        private enum Operation
        {
            START,
            USER, //登录FTP的用户名
            PASS, //登录FTP的密码
            PWD,
            CWD,
            PASV, //进入被动模式，返回server的数据端口，等待client连接    
            LIST, //查看服务器文件（从数据端口返回结果）
            RETR, //请求下载
            STOR, //请求上传
            quit
        }

        static readonly Dictionary<Operation, string[]> Arguments = new(
            new KeyValuePair<Operation, string[]>[]
            {
                new(Operation.START, Array.Empty<string>()),
                new(Operation.USER, new string[1] {"Username"}),
                new(Operation.PASS, new string[1] {"Password"}),
                new(Operation.PWD, new string[1] {"P Working Directory"}),
                new(Operation.CWD, new string[1] {"Current Working Directory"}),
                new(Operation.PASV, Array.Empty<string>()),
                new(Operation.LIST, Array.Empty<string>()),
                new(Operation.RETR, new string[1] {"Path"}),
                new(Operation.STOR, new string[1] {"Upload"}),
                new(Operation.quit, Array.Empty<string>())
            });

        public void Shell()
        {
            bool quit = false;
            while (!quit)
            {
                Console.Write(">");
                var args = Console.ReadLine()?.Split(' ');
                if (args is { Length: 0 })
                    continue;
                if (!Enum.TryParse(args[0], true, out Operation op))
                {
                    Console.WriteLine(
                        "Invalid operation. Type 'help' to show help.");
                    continue;
                }

                if (args.Length != Arguments[op].Length + 1)
                {
                    Console.WriteLine(
                        "Argument count error. Type 'help' to show help.");
                    continue;
                }

                switch (op)
                {
                    case Operation.USER:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"USER {args[1]}\r\n");
                        break;
                    case Operation.PASS:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"PASS {args[1]}\r\n");
                        break;
                    case Operation.PWD:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"PWD {args[1]}\r\n");
                        break;
                    case Operation.CWD:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"CWD {args[1]}\r\n");
                        break;
                    case Operation.PASV:
                        Send("PASV\r\n");
                        break;
                    case Operation.LIST:
                        CommandLIST();
                        break;
                    case Operation.RETR:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }
                        CommandRETR(args[1]);
                        break;
                    default:
                        Console.WriteLine("Unknown Command");
                        break;
                }
            }
        }


        public void Parse(string cli)
        {

            var args = cli?.Split(' ');
            if (args is { Length: 0 })
                return;
            if (!Enum.TryParse(args[0], true, out Operation op))
            {
                Console.WriteLine(
                    "Invalid operation. Type 'help' to show help.");
                return;
            }

            if (args.Length != Arguments[op].Length + 1)
            {
                Console.WriteLine(
                    "Argument count error. Type 'help' to show help.");
                return;
            }

            switch (op)
            {
                case Operation.START:
                    Initialize2();
                    break;
                case Operation.USER:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"USER {args[1]}\r\n");
                    break;
                case Operation.PASS:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"PASS {args[1]}\r\n");
                    break;
                case Operation.PWD:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"PWD {args[1]}\r\n");
                    break;
                case Operation.CWD:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"CWD {args[1]}\r\n");
                    break;
                case Operation.PASV:
                    Send("PASV\r\n");
                    break;
                case Operation.LIST:
                    CommandLIST();
                    break;
                case Operation.RETR:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }
                    CommandRETR(args[1]);
                    break;
                default:
                    Console.WriteLine("Unknown Command");
                    break;
            }

        }

        public void CommandLIST()
        {
            Flush();
            if (_pasvport == -1)
            {
                Console.WriteLine("need PASV");
                return;
            }

            // start a TCP
            TcpClient client = null;
            try
            {
                client = new TcpClient(Hostname, _pasvport);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _pasvport = -1;
            NetworkStream getStream = null;
            try
            {
                getStream = client.GetStream();
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _sendOffset = 0;
            _recOffset = 0;

            var receiveData = new byte[256];
            //Send("LIST\r\n");
            Parallel.Invoke(() => Send($"LIST\r\n"));
            // wait for a response
            var dataLength =
                getStream.Read(receiveData, _recOffset, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            _recOffset += dataLength;

            // Begin to List 
            //Console.WriteLine($"Sending LIST");
            //var data = System.Text.Encoding.ASCII.GetBytes("LIST\r\n");
            //stream.Write(data, 0, data.Length);
            //sendOffset += data.Length;



            // wait for a response
            while (getStream.DataAvailable)
            {
                receiveData = new byte[256];
                dataLength = getStream.Read(receiveData, 0, receiveData.Length);
                recvdMessage =
                    System.Text.Encoding.ASCII.GetString(receiveData, 0,
                        dataLength);
                Console.WriteLine(recvdMessage.ToString());
            }


            Flush();
        }


        public void CommandRETR(string path)
        {
            Flush();
            if (_pasvport == -1)
            {
                Console.WriteLine("need PASV");
                return;
            }

            // start a TCP
            TcpClient client = null;
            try
            {
                client = new TcpClient(Hostname, _pasvport);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _pasvport = -1;
            NetworkStream getStream = null;
            try
            {
                getStream = client.GetStream();
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            _sendOffset = 0;
            _recOffset = 0;

            var receiveData = new byte[256];

            // Begin to RETR 
            Parallel.Invoke(() => Send($"RETR {path}\r\n"));

            // wait for a response
            var dataLength =
                getStream.Read(receiveData, _recOffset, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            _recOffset += dataLength;


            // wait for a response
            while (getStream.DataAvailable)
            {
                receiveData = new byte[256];
                dataLength = getStream.Read(receiveData, 0, receiveData.Length);
                recvdMessage =
                    System.Text.Encoding.ASCII.GetString(receiveData, 0,
                        dataLength);
                Console.WriteLine(recvdMessage.ToString());
            }


            Flush();
        }


    }
}