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
        private IpProtocal ipProtocal;
        private FtpClient ftpClient;
        private TcpClient tcpClient;
        private NetworkStream stream;
        private int sendOffset;
        private int recOffset;
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
                tcpClient = new TcpClient("127.0.0.1", 21);

            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
            }
            stream = tcpClient.GetStream();
            sendOffset = 0;
            recOffset = 0;

            var receiveData = new byte[256];

            // wait for a response
            var dataLength = stream.Read(receiveData, recOffset, receiveData.Length);
            var recvdMessage = System.Text.Encoding.ASCII.GetString(receiveData, 0, dataLength);
            Console.WriteLine(recvdMessage.ToString());
            recOffset += dataLength;
        }

        private void Send(string message)
        {
            Console.WriteLine($"Sending {message}");
            var data = System.Text.Encoding.ASCII.GetBytes(message);
            stream.Write(data, 0, data.Length);
            sendOffset += data.Length;

            // wait for a response
            while (!stream.DataAvailable)
            {
            }
            var receiveData = new byte[256];
            var dataLength = stream.Read(receiveData, 0, receiveData.Length);
            var recvdMessage = System.Text.Encoding.ASCII.GetString(receiveData, 0, dataLength);
            Console.WriteLine(recvdMessage.ToString());
        }



        private enum Operation
        {
            USER,  //登录FTP的用户名
            PASS,  //登录FTP的密码
            PWD,
            CWD,
            PASV,  //进入被动模式，返回server的数据端口，等待client连接    
            LIST,  //查看服务器文件（从数据端口返回结果）
            RETR,  //请求下载
            STOR,  //请求上传
            quit
        }

        static readonly Dictionary<Operation, string[]> Arguments = new(
            new KeyValuePair<Operation, string[]>[]
            {
                new(Operation.USER, new string[1] { "Username" }),
                new(Operation.PASS, new string[1] { "Password" }),
                new(Operation.PWD, new string[1] { "P Working Directory" }),
                new(Operation.CWD, new string[1] { "Current Working Directory" }),
                new(Operation.PASV, Array.Empty<string>()),
                new(Operation.LIST, Array.Empty<string>()),
                new(Operation.RETR, new string[1] { "Downlaod" }),
                new(Operation.STOR, new string[1] { "Upload" }),
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
                        Send("LIST\r\n");
                        break;
                    default:
                        break;
                }
            }
        }


        public void CommandUser(string username)
        {
            Send($"USER {username}\r\n");
        }

    }
}
