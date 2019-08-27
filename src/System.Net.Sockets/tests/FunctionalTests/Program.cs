using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace udp_bench
{
    class Program
    {
        const int BufferLen = 500;
        const int ClientCount = 10;
        const int RequestsPerClient = 10000;

        static void Main(string[] args)
        {
            IPEndPoint defaultRecvEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

            // init server.

            using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            byte[] serverBuffer = new byte[BufferLen];

            serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            EndPoint serverEndPoint = serverSocket.LocalEndPoint;

            // wait for profiler to attach.

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();

            // start clients.

            Task[] clients = new Task[ClientCount];

            for (int i = 0; i < ClientCount; ++i)
            {
                clients[i] = StartClient();
            }

            // run server.
            SocketAddress clientAddress = defaultRecvEndPoint.Serialize();

            for (int i = 0; i < (ClientCount * RequestsPerClient); ++i)
            {
                clientAddress.SetAddress(defaultRecvEndPoint);
                int len = serverSocket.ReceiveFrom(serverBuffer, 0, serverBuffer.Length, SocketFlags.None, clientAddress);

                serverSocket.SendTo(serverBuffer, 0, len, SocketFlags.None, clientAddress);
            }

            // wait for clients to exit.
            Task.WhenAll(clients).Wait();

            Process p = Process.GetCurrentProcess();
            Console.WriteLine($"User time: {p.UserProcessorTime}");

            Task StartClient() =>
                Task.Factory.StartNew(() =>
                {
                    using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    SocketAddress serverAddress = serverEndPoint.Serialize();
                    byte[] clientBuffer = new byte[BufferLen];

                    clientSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

                    for (int i = 0; i < RequestsPerClient; ++i)
                    {
                        clientSocket.SendTo(clientBuffer, 0, clientBuffer.Length, SocketFlags.None, serverAddress);
                        clientSocket.ReceiveFrom(clientBuffer, 0, clientBuffer.Length, SocketFlags.None, serverAddress);
                    }
                }, TaskCreationOptions.LongRunning);
        }
    }
}
