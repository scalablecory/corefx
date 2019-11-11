using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    internal sealed class SocketConnection : IConnection, IConnectionProperties
    {
        private readonly NetworkStreamWithSocket _stream;

        public EndPoint RemoteEndPoint => _stream.Socket.RemoteEndPoint;
        public EndPoint LocalEndPoint => _stream.Socket.LocalEndPoint;
        public IConnectionProperties ConnectionProperties => this;
        public Stream Stream => _stream;

        public SocketConnection(Socket socket)
        {
            _stream = new NetworkStreamWithSocket(socket);
        }

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }

        public void CompleteRead()
        {
            _stream.Socket.Shutdown(SocketShutdown.Receive);
        }

        public void CompleteWrite()
        {
            _stream.Socket.Shutdown(SocketShutdown.Send);
        }

        bool IConnectionProperties.TryGet(Type propertyType, out object property)
        {
            if (propertyType == typeof(Socket))
            {
                property = _stream.Socket;
                return true;
            }

            property = null;
            return false;
        }
    }
}
