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
using System.Threading;
using System.Threading.Tasks;

namespace get1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            byte[] payload = File.ReadAllBytes(args[0]);

            using Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listenSocket.Listen(1);

            var localEndPoint = (IPEndPoint)listenSocket.LocalEndPoint;
            string uri = $"https://{localEndPoint.Address}:{localEndPoint.Port}/";

            var handler = new SocketsHttpHandler()
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                }
            };

            using HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = new Version(2, 0);

            Task clientTask = client.GetStringAsync(uri);

            using Socket acceptSock = await listenSocket.AcceptAsync();
            using Stream acceptStream = new NetworkStream(acceptSock, true);
            using SslStream sslStream = new SslStream(acceptStream, false, delegate { return true; });

            using (var cert = System.Net.Test.Common.Configuration.Certificates.GetServerCertificate())
            {
                SslServerAuthenticationOptions options = new SslServerAuthenticationOptions();

                options.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                var protocols = new List<SslApplicationProtocol>();
                protocols.Add(SslApplicationProtocol.Http2);
                protocols.Add(SslApplicationProtocol.Http11);
                options.ApplicationProtocols = protocols;

                options.ServerCertificate = cert;

                options.ClientCertificateRequired = false;

                await sslStream.AuthenticateAsServerAsync(options);
            }

            // read once so we don't send stream data before HTTPClient has initialized its own streams.
            byte[] readbuf = new byte[1024];
            await sslStream.ReadAsync(readbuf.AsMemory());

            _ = DoRead();
            _ = sslStream.WriteAsync(payload.AsMemory());

            await clientTask;

            async Task DoRead()
            {
                int len;

                do len = await sslStream.ReadAsync(readbuf.AsMemory());
                while (len != 0);
            }
        }
    }
}
