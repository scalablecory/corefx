using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Test.Common;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace get1
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] payload = File.ReadAllBytes(args[0]);

            var opts = new LoopbackServer.Options { UseSsl = true };
            LoopbackServer.CreateClientAndServerAsync(async uri =>
            {
                var handler = new SocketsHttpHandler()
                {
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = delegate { return true; }
                    }
                };
                using HttpClient client = new HttpClient(handler);
                client.DefaultRequestVersion = new Version(2, 0);

                await client.GetStringAsync(uri);
            },
            async server =>
            {
                using LoopbackServer.Connection connection = await server.EstablishConnectionAsync();

                Task readTask = connection.WaitForCancellationAsync();
                Task writeTask = DoWrite();

                await new[] { readTask, writeTask }.WhenAllOrAnyFailed();

                async Task DoWrite()
                {
                    await connection.Stream.WriteAsync(payload);
                    await connection.Stream.FlushAsync();
                    connection.Socket.Shutdown(System.Net.Sockets.SocketShutdown.Send);
                }
            }, opts).GetAwaiter().GetResult();
        }
    }
}
