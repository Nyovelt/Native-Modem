using System;
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

            if (_ipProtocal.Node == "2")
            {
                _ipProtocal.IP = Hostname;
            }
            var flag = true;
            while (true) { }
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

                NetworkStream stream = client.GetStream();


                while (_ipProtocal.flag == false) { }

                while (_ipProtocal.savedData.TryDequeue(out var savedData))
                {
                    // Send back a response.
                    stream.Write(savedData, 0, savedData.Length);
                    Console.WriteLine("Sent: {0}", System.Text.Encoding.ASCII.GetString(savedData, 0, savedData.Length));
                }
                _ipProtocal.flag = false;
                int i;

                // Loop to receive all the data sent by the client.
                while (true)
                {
                    i = stream.Read(bytes, 0, bytes.Length);
                    // Translate data bytes to a ASCII string.
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Received: {0}", data);
                    byte[] temp = new byte[i];
                    Array.Copy(bytes, temp, i);
                    _ipProtocal.Modem.TransportData(2, temp);
                    while (_ipProtocal.flag == false) { }

                    // Process the data sent by the client.
                    while (_ipProtocal.savedData.TryDequeue(out var savedData))
                    {
                        // Send back a response.
                        stream.Write(savedData, 0, savedData.Length);
                        Console.WriteLine("Sent: {0}", System.Text.Encoding.ASCII.GetString(savedData, 0, savedData.Length));
                    }
                    _ipProtocal.flag = false;

                }
            }
        }

        private void pasvTunnel(int port)
        {
            // Start TCP Listener
            IPAddress localAddr = IPAddress.Parse(_ipProtocal.IP); //or 127.0.0.1
            var listener = new TcpListener(localAddr, port);
            listener.Start();

            // Buffer for reading data
            var bytes = new byte[256];
            string data = null;


            // Perform a blocking call to accept requests.
            // You could also use server.AcceptSocket() here.
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine("Connected!");

            data = null;

            // Get a stream object for reading and writing

            NetworkStream stream = client.GetStream();


            while (_ipProtocal.flag2 == false) { }

            while (_ipProtocal.savedData2.TryDequeue(out var savedData))
            {
                // Send back a response.
                stream.Write(savedData, 0, savedData.Length);
                Console.WriteLine("Sent: {0}", System.Text.Encoding.ASCII.GetString(savedData, 0, savedData.Length));
                Thread.Sleep(2000);
            }
            _ipProtocal.flag2 = false;

        }

        private void Send(string message)
        {
            //message = message.Substring(0, Math.Max(0, message.IndexOf('\0')));
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
            if (_ipProtocal.Node == "2")
            {
                _ipProtocal.Modem.TransportData(1, Encoding.ASCII.GetBytes(ret));
            }






            if (message == "PASV\r\n" && ret.Split(' ')[0] == "227")
            {
                ret = (ret.Split(' ')[4]).Replace("\r", "").Replace("\n", "")
                    .Replace("(", "").Replace(")", "").Replace(".", "");
                Console.WriteLine("PASV port changed to {0}",
                    int.Parse(ret.Split(',')[4]) * 256 +
                    int.Parse(ret.Split(',')[5]));
                _pasvport = int.Parse(ret.Split(',')[4]) * 256 +
                            int.Parse(ret.Split(',')[5]);
                if (_ipProtocal.Node == "1")
                {
                    Thread t = new Thread(() => pasvTunnel(_pasvport));
                    t.Start();
                }

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
                if (_ipProtocal.Node == "2")
                {
                    var temp = new byte[dataLength];
                    Array.Copy(receiveData, temp, dataLength);
                    _ipProtocal.Modem.TransportData(1, temp);
                }
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
            SYST,
            FEAT,
            TYPE,
            MLSD,
            quit
        }

        static readonly Dictionary<Operation, string[]> Arguments = new(
            new KeyValuePair<Operation, string[]>[]
            {
                new(Operation.START, Array.Empty<string>()),
                new(Operation.USER, new string[1] {"Username"}),
                new(Operation.PASS, new string[1] {"Password"}),
                new(Operation.PWD, Array.Empty<string>()),
                new(Operation.CWD, new string[1] {"Current Working Directory"}),
                new(Operation.PASV, Array.Empty<string>()),
                new(Operation.LIST, Array.Empty<string>()),
                new(Operation.RETR, new string[1] {"Path"}),
                new(Operation.TYPE, new string[1] {"ASCII"}),
                new(Operation.STOR, new string[1] {"Upload"}),
                new(Operation.SYST, Array.Empty<string>()),
                new(Operation.MLSD, Array.Empty<string>()),
                new(Operation.FEAT, Array.Empty<string>()),
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


                        Send("PWD\r\n");
                        break;
                    case Operation.SYST:
                        Send($"SYST\r\n");
                        break;
                    case Operation.FEAT:
                        Send($"FEAT\r\n");
                        break;
                    case Operation.CWD:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"CWD {args[1]}\r\n");
                        break;
                    case Operation.TYPE:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }

                        Send($"TYPE {args[1]}\r\n");
                        break;
                    case Operation.PASV:
                        Send("PASV\r\n");
                        break;
                    case Operation.LIST:
                        CommandPASV($"LIST\r\n");
                        break;
                    case Operation.MLSD:
                        CommandPASV($"MLSD\r\n");
                        break;
                    case Operation.RETR:
                        if (args[1] == null)
                        {
                            Console.WriteLine("Invalid arguments");
                            break;
                        }
                        CommandPASV($"RETR {args[1]}\r\n");
                        break;
                    default:
                        Console.WriteLine("Unknown Command");
                        break;
                }
            }
        }


        public void Parse(string cli)
        {

            var args = cli?.Replace("\r", "").Replace("\n", "").Split(' ');
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

                    Send("PWD\r\n");
                    break;
                case Operation.CWD:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"CWD {args[1]}\r\n");
                    break;
                case Operation.TYPE:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }

                    Send($"TYPE {args[1]}\r\n");
                    break;
                case Operation.SYST:
                    Send($"SYST\r\n");
                    break;
                case Operation.FEAT:
                    Send($"FEAT\r\n");
                    break;
                case Operation.PASV:
                    Send("PASV\r\n");
                    break;
                case Operation.LIST:
                    CommandPASV($"LIST\r\n");
                    break;
                case Operation.MLSD:
                    CommandPASV($"MLSD\r\n");
                    break;
                case Operation.RETR:
                    if (args[1] == null)
                    {
                        Console.WriteLine("Invalid arguments");
                        break;
                    }
                    CommandPASV($"RETR {args[1]}\r\n");
                    break;
                default:
                    Console.WriteLine("Unknown Command");
                    break;
            }

        }

        public void CommandPASV(string cli)
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
                client = new TcpClient(_ipProtocal.IP, _pasvport);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                _pasvport = -1;
            }


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
            Parallel.Invoke(() => Send(cli));

            // wait for a response
            var dataLength =
                getStream.Read(receiveData, _recOffset, receiveData.Length);
            var recvdMessage =
                System.Text.Encoding.ASCII.GetString(receiveData, 0,
                    dataLength);
            Console.WriteLine(recvdMessage.ToString());
            var temp = new byte[dataLength + 1];
            Array.Copy(receiveData, 0, temp, 1, dataLength);
            temp[0] = 0xfe;
            if (_ipProtocal.Node == "2")
                _ipProtocal.Modem.TransportData(1, temp);
            _recOffset += dataLength;
            if (_ipProtocal.Node == "1")
                Thread.Sleep(3000);


            // wait for a response
            while (getStream.DataAvailable)
            {
                receiveData = new byte[256];
                dataLength = getStream.Read(receiveData, 0, receiveData.Length);
                recvdMessage =
                    System.Text.Encoding.ASCII.GetString(receiveData, 0,
                        dataLength);
                Console.WriteLine(recvdMessage.ToString());
                temp = new byte[dataLength + 1];
                Array.Copy(receiveData, 0, temp, 1, dataLength);
                temp[0] = 0xfe;
                if (_ipProtocal.Node == "2")
                    _ipProtocal.Modem.TransportData(1, temp);
                if (_ipProtocal.Node == "1")
                    Thread.Sleep(3000);
            }
            _pasvport = -1;

            Flush();
        }




    }
}