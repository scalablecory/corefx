using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace get1
{
    class Program
    {
        static void Main(string[] args)
        {
            // 2008 R2 on MSRD does not support ALPN.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            if (!File.GetAttributes(args[0]).HasFlag(FileAttributes.Directory))
            {
                RunOneImpl(args[0]).GetAwaiter().GetResult();
            }
            else
            {
                var exceptions =
                    from filePath in Directory.EnumerateFiles(args[0], "*.bin")
                    from exception in RunOne(filePath)
                    group (exception, filePath) by exception.ToString() into grp
                    select grp
                        .OrderByDescending(ex_fp => new FileInfo(ex_fp.filePath).Length)
                        .First();

                foreach (var (exception, filePath) in exceptions)
                {
                    Console.WriteLine(filePath);
                    Console.WriteLine("===============");
                    Console.WriteLine(exception.ToString());
                    Console.WriteLine();
                }
            }
        }

        static IEnumerable<Exception> RunOne(string filePath)
        {
            try
            {
                RunOneImpl(filePath).Wait();
                return Enumerable.Empty<Exception>();
            }
            catch (AggregateException ex)
            {
                return ex.Flatten().InnerExceptions;
            }
            catch (Exception ex)
            {
                return new[] { ex };
            }
        }

        static async Task RunOneImpl(string filePath)
        {
            byte[] payload = File.ReadAllBytes(filePath);

            using Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listenSocket.Listen(1);

            var localEndPoint = (IPEndPoint)listenSocket.LocalEndPoint;
            string uri = $"http://{localEndPoint.Address}:{localEndPoint.Port}/";

            var handler = new SocketsHttpHandler()
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                },
                UseProxy = false
            };

            using HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = new Version(2, 0);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Add("x-foo-ready", "1");

            Task clientTask = client.GetStringAsync(uri);

            using Socket acceptSock = await listenSocket.AcceptAsync();
            using Stream acceptStream = new NetworkStream(acceptSock, true);

            await DoReadUntilHeaders();
            _ = DoRead();
            _ = DoWrite();

            try
            {
                await clientTask;
            }
            catch (HttpRequestException ex) when (IsExpectedException(ex.InnerException))
            {
                // ignore protocol exceptions.
            }

            bool IsExpectedException(Exception ex)
            {
                while (ex is IOException)
                {
                    ex = ex.InnerException;
                }

                if (ex == null)
                {
                    return false;
                }

                if (ex.GetType().Name == "Http2ConnectionException")
                    return true;
                if (ex.GetType().Name == "Http2StreamException")
                    return true;
                return false;
            }

            async Task DoReadUntilHeaders()
            {
                byte[] expected = Encoding.ASCII.GetBytes("x-foo-ready");
                byte[] readbuf = new byte[1024];
                int readpos = 0;

                while (true)
                {
                    if (readpos == readbuf.Length)
                    {
                        throw new Exception("Never received headers.");
                    }

                    int len = await acceptStream.ReadAsync(readbuf.AsMemory(readpos));

                    if (len == 0)
                    {
                        throw new Exception("Never received headers.");
                    }

                    readpos += len;

                    for (int i = 0; i < readpos; ++i)
                    {
                        if (readbuf.AsSpan(i, Math.Min(readpos - i, expected.Length)).SequenceEqual(expected))
                        {
                            return;
                        }
                    }

                }
            }

            async Task DoRead()
            {
                byte[] readbuf = new byte[1024];
                int len;

                do
                    len = await acceptStream.ReadAsync(readbuf.AsMemory());
                while (len != 0);
            }

            async Task DoWrite()
            {
                await acceptStream.WriteAsync(payload.AsMemory());
                await acceptStream.FlushAsync();
                acceptSock.Shutdown(SocketShutdown.Send);
            }
        }
    }
}
