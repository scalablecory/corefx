using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Test.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace createseeds
{
    class Program
    {
        static void Main(string[] args)
        {
            Http2LoopbackServer.CreateClientAndServerAsync(async uri =>
            {
                var handler = new SocketsHttpHandler()
                {
                    StreamWrapper = s => new StreamDumper(s)
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

                await connection.SendDefaultResponseAsync(streamId);
                await connection.ShutdownAsync(streamId);
            }).GetAwaiter().GetResult();
        }
    }

    sealed class StreamDumper : Stream
    {
        static int s_count;
        readonly Stream _baseStream, _readStream;

        public StreamDumper(Stream baseStream)
        {
            _baseStream = baseStream;

            int id = Interlocked.Increment(ref s_count);
            _readStream = new FileStream($"connection_{id:N4}_server.bin", FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _readStream.Dispose();
                _baseStream.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await _readStream.DisposeAsync().ConfigureAwait(false);
            await _baseStream.DisposeAsync().ConfigureAwait(false);
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

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _readStream.WriteAsync(buffer.Slice(0, count), cancellationToken).ConfigureAwait(false);
            await _readStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return count;
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
            throw new NotImplementedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _baseStream.WriteAsync(buffer, cancellationToken);
        }
    }
}
