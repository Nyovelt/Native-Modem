using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;

namespace Native_Modem
{

    public class NaiveFtp
    {
        private IpProtocal _ipProtocal;
        private FtpClient _ftpClient;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private int _sendOffset;
        private int _recOffset;
        private const string Hostname = "127.0.0.1";
        private int _pasvport = -1;

        public NaiveFtp()
        {
            //ipProtocal = new IpProtocal();
            //ftpClient = new FtpClient("127.0.0.1", 9000, "ftptest", "ftptest");

            Initialize();
            Shell();
        }

        private void Initialize()
        {
            try
            {
                _tcpClient = new TcpClient(Hostname, 21);

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

        private void Send(string message)
        {
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





            if (message == "PASV\r\n")
            {
                ret = (ret.Split(' ')[4]).Replace("\r", "").Replace("\n", "")
                    .Replace("(", "").Replace(")", "");
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
            }
        }

        private enum Operation
        {
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


        public void CommandLIST()
        {
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
            while (!getStream.DataAvailable)
            {
            }

            receiveData = new byte[256];
            dataLength = getStream.Read(receiveData, 0, receiveData.Length);
            recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            Flush();
        }


        public void CommandRETR(string path)
        {
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
            while (!getStream.DataAvailable)
            {
            }

            receiveData = new byte[256];
            dataLength = getStream.Read(receiveData, 0, receiveData.Length);
            recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            Flush();
        }


    }
}