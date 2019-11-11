using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Connections
{
    public sealed class SocketsConnectionFactory : IConnectionFactory
    {
        private readonly SocketType _socketType;
        private readonly ProtocolType _protocolType;

        private readonly ConcurrentQueueSegment<TaskSocketAsyncEventArgs> _connectArgsPool = new ConcurrentQueueSegment<TaskSocketAsyncEventArgs>(boundedLength: 32);
        private volatile bool _disposed;

        public SocketsConnectionFactory(SocketType socketType, ProtocolType protocolType)
        {
            _socketType = socketType;
            _protocolType = protocolType;
        }

        public async ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (_disposed) throw new ObjectDisposedException(nameof(SocketsConnectionFactory));

            if (!_connectArgsPool.TryDequeue(out TaskSocketAsyncEventArgs args))
            {
                args = new TaskSocketAsyncEventArgs();
            }

            try
            {
                args.RemoteEndPoint = endPoint;

                if (Socket.ConnectAsync(_socketType, _protocolType, args))
                {
                    using (cancellationToken.UnsafeRegister(o => Socket.CancelConnectAsync((SocketAsyncEventArgs)o), args))
                    {
                        await args.Task.ConfigureAwait(false);
                    }
                }

                if (args.SocketError != SocketError.Success)
                {
                    if (args.SocketError == SocketError.OperationAborted && cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new SocketException((int)args.SocketError);
                }

                return new SocketConnection(args.ConnectSocket);
            }
            finally
            {
                args.ResetTask();
                if (!_connectArgsPool.TryEnqueue(args))
                {
                    args.Dispose();
                }

                if (_disposed)
                {
                    FlushPool();
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            FlushPool();
            return default;
        }

        private void FlushPool()
        {
            while (_connectArgsPool.TryDequeue(out TaskSocketAsyncEventArgs args))
            {
                args.Dispose();
            }
        }
    }
}
