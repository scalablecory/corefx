using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.Test.Common;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace get1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 2008 R2 on MSRD does not support ALPN.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            byte[] payload = File.ReadAllBytes(args[0]);

            using Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listenSocket.Listen(1);

            var localEndPoint = (IPEndPoint)listenSocket.LocalEndPoint;
            string uri = $"http://{localEndPoint.Address}:{localEndPoint.Port}/";
            Console.WriteLine($"Testing via {uri}");

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
            //using SslStream sslStream = new SslStream(acceptStream, false, delegate { return true; });

            //using (var cert = System.Net.Test.Common.Configuration.Certificates.GetServerCertificate())
            //{
            //    SslServerAuthenticationOptions options = new SslServerAuthenticationOptions();

            //    options.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            //    var protocols = new List<SslApplicationProtocol>();
            //    protocols.Add(SslApplicationProtocol.Http2);
            //    protocols.Add(SslApplicationProtocol.Http11);
            //    options.ApplicationProtocols = protocols;

            //    options.ServerCertificate = cert;

            //    options.ClientCertificateRequired = false;

            //    await sslStream.AuthenticateAsServerAsync(options);
            //}

            await DoReadUntilHeaders();
            _ = DoRead();
            _ = DoWrite();

            try
            {
                Console.WriteLine("Waiting for HttpClient...");
                await clientTask;
            }
            catch (HttpRequestException ex) when (IsExpectedException(ex.InnerException))
            {
                // ignore protocol exceptions.
            }
            finally
            {
                Console.WriteLine("Done with HttpClient.");
            }

            bool IsExpectedException(Exception ex)
            {
                if (ex.GetType().Name == "Http2ConnectionException")
                    return true;
                if (ex.GetType().Name == "Http2StreamException")
                    return true;
                return false;
            }

            async Task DoReadUntilHeaders()
            {
                Console.WriteLine("Waiting for headers...");
                try
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
                                Console.WriteLine("Found headers.");
                                return;
                            }
                        }

                    }
                }
                finally
                {
                    Console.WriteLine("Done waiting for headers.");
                }
            }

            async Task DoRead()
            {
                Console.WriteLine("Reading...");
                try
                {
                    byte[] readbuf = new byte[1024];
                    int len;

                    do
                        len = await acceptStream.ReadAsync(readbuf.AsMemory());
                    while (len != 0);
                }
                finally
                {
                    Console.WriteLine("Done Reading.");
                }
            }

            async Task DoWrite()
            {
                Console.WriteLine("Writing...");
                try
                {
                    // Wait for HttpClient to initiate its stream ID, or writing payload will fail.
                    await Task.Delay(500);

                    await acceptStream.WriteAsync(payload.AsMemory());
                    await acceptStream.FlushAsync();
                    //await sslStream.ShutdownAsync();
                    acceptSock.Shutdown(SocketShutdown.Send);
                }
                finally
                {
                    Console.WriteLine("Done Writing.");
                }
            }
        }
    }
}
