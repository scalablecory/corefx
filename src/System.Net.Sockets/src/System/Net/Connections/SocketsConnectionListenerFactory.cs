using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public sealed class SocketsConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly SocketType _socketType;
        private readonly ProtocolType _protocolType;

        public ValueTask<IConnectionListener> BindAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (cancellationToken.IsCancellationRequested) return new ValueTask<IConnectionListener>(Task.FromCanceled<IConnectionListener>(cancellationToken));

            Socket socket;

            try
            {
                socket = new Socket(endPoint.AddressFamily, _socketType, _protocolType);
            }
            catch (Exception ex)
            {
                return new ValueTask<IConnectionListener>(Task.FromException<IConnectionListener>(ex));
            }

            try
            {
                socket.Bind(endPoint);
                socket.Listen();
                return new ValueTask<IConnectionListener>(new SocketsConnectionListener(socket));
            }
            catch (Exception ex)
            {
                socket.Dispose();
                return new ValueTask<IConnectionListener>(Task.FromException<IConnectionListener>(ex));
            }
        }

        public SocketsConnectionListenerFactory(SocketType socketType, ProtocolType protocolType)
        {
            _socketType = socketType;
            _protocolType = protocolType;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        private sealed class SocketsConnectionListener : IConnectionListener
        {
            private readonly Socket _socket;
            private readonly ConcurrentQueueSegment<TaskSocketAsyncEventArgs> _acceptArgsPool = new ConcurrentQueueSegment<TaskSocketAsyncEventArgs>(boundedLength: 32);
            private volatile bool _disposed;

            public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SocketsConnectionFactory));

                if (!_acceptArgsPool.TryDequeue(out TaskSocketAsyncEventArgs args))
                {
                    args = new TaskSocketAsyncEventArgs();
                }

                try
                {
                    args.AcceptSocket = new Socket(_socket.AddressFamily, _socket.SocketType, _socket.ProtocolType);

                    if (_socket.AcceptAsync(args))
                    {
                        using (cancellationToken.UnsafeRegister(o => ((SocketAsyncEventArgs)o).AcceptSocket.Dispose(), args))
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

                    var con = new SocketConnection(args.AcceptSocket);
                    args.AcceptSocket = null;
                    return con;
                }
                finally
                {
                    if (args.AcceptSocket != null)
                    {
                        args.AcceptSocket.Dispose();
                        args.AcceptSocket = null;
                    }

                    args.ResetTask();

                    if (!_acceptArgsPool.TryEnqueue(args))
                    {
                        args.Dispose();
                    }

                    if (_disposed)
                    {
                        FlushPool();
                    }
                }
            }

            public SocketsConnectionListener(Socket socket)
            {
                _socket = socket;
            }

            public ValueTask DisposeAsync()
            {
                try
                {
                    _disposed = true;
                    FlushPool();

                    _socket.Dispose();
                    return default;
                }
                catch (Exception ex)
                {
                    return new ValueTask(Task.FromException(ex));
                }
            }

            private void FlushPool()
            {
                while (_acceptArgsPool.TryDequeue(out TaskSocketAsyncEventArgs args))
                {
                    args.Dispose();
                }
            }
        }
    }
}
