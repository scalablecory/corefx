using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Test.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace createseeds
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await DoGet(async (con, streamId) =>
            {
                await con.SendDefaultResponseAsync(streamId);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders | FrameFlags.EndStream, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndStream, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-next-frame", "asdf"), buffer);
                frame = new ContinuationFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.None, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-next-frame", "asdf"), buffer);
                frame = new ContinuationFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                frame = new DataFrame(ReadOnlyMemory<byte>.Empty, FrameFlags.EndStream, 0, streamId);
                await con.WriteFrameAsync(frame);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.None, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-next-frame", "asdf"), buffer);
                frame = new ContinuationFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                Array.Fill(buffer, (byte)0);
                frame = new DataFrame(buffer.AsMemory(), FrameFlags.EndStream, 0, streamId);
                await con.WriteFrameAsync(frame);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                Array.Fill(buffer, (byte)0);
                frame = new DataFrame(buffer.AsMemory(), FrameFlags.None, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-trailing-header", "qwerty"), buffer);
                frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndStream, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-next-trailing-frame", "asdf"), buffer);
                frame = new ContinuationFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);
            });

            await DoGet(async (con, streamId) =>
            {
                byte[] buffer = new byte[4096];
                int len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData(":status", "200"), buffer);
                Frame frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                Array.Fill(buffer, (byte)0);
                frame = new DataFrame(buffer.AsMemory(0, 1024), FrameFlags.None, 0, streamId);
                await con.WriteFrameAsync(frame);

                Array.Fill(buffer, (byte)0);
                frame = new DataFrame(buffer.AsMemory(0, 2048), FrameFlags.None, 0, streamId);
                await con.WriteFrameAsync(frame);

                Array.Fill(buffer, (byte)0);
                frame = new DataFrame(buffer.AsMemory(0, 4096), FrameFlags.None, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-trailing-header", "qwerty"), buffer);
                frame = new HeadersFrame(buffer.AsMemory(0, len), FrameFlags.EndStream, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);

                len = Http2LoopbackConnection.EncodeHeader(new HttpHeaderData("x-next-trailing-frame", "asdf"), buffer);
                frame = new ContinuationFrame(buffer.AsMemory(0, len), FrameFlags.EndHeaders, 0, 0, 0, streamId);
                await con.WriteFrameAsync(frame);
            });
        }

        static async Task DoGet(Func<Http2LoopbackConnection, int, Task> run)
        {
            Http2Options opts = new Http2Options() { StreamWrapper = s => new StreamDumper(s) };

            await Http2LoopbackServer.CreateClientAndServerAsync(async uri =>
            {
                var handler = new SocketsHttpHandler()
                {
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = delegate { return true; }
                    }
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestVersion = new Version(2, 0);

                using HttpResponseMessage response = await client.GetAsync(uri);
                await response.Content.ReadAsStringAsync();
            },
            async server =>
            {
                using Http2LoopbackConnection connection = await server.EstablishConnectionAsync();
                int streamId = await connection.ReadRequestHeaderAsync();
                await run(connection, streamId);
            }, options: opts);
        }
    }

    sealed class StreamDumper : Stream
    {
        static int s_count = 1;
        readonly Stream _baseStream, _logStream;

        public StreamDumper(Stream baseStream)
        {
            _baseStream = baseStream;

            int id = Interlocked.Increment(ref s_count);
            _logStream = new FileStream($"seed_{id}.bin", FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);

            Console.Write($"byte[] seed_{id} = new byte[] {{");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _logStream.Dispose();
                _baseStream.Dispose();
                Console.WriteLine(" }");
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await _logStream.DisposeAsync().ConfigureAwait(false);
            await _baseStream.DisposeAsync().ConfigureAwait(false);
            Console.WriteLine(" }");
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _baseStream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);
            _logStream.Write(buffer, offset, count);
            _logStream.Flush();

            WriteHex(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            await _logStream.WriteAsync(buffer, offset, count, cancellationToken);
            await _logStream.FlushAsync(cancellationToken);

            WriteHex(buffer, offset, count);
        }

        int pos = 0;

        void WriteHex(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                Console.Write(pos++ == 0 ? " 0x" : ", 0x");
                Console.Write(buffer[offset + i].ToString("X2"));
            }
        }
    }
}
