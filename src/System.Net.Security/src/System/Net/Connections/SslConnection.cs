using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    internal sealed class SslConnection : IConnection, IConnectionProperties
    {
        private readonly IConnection _baseConnection;
        private readonly SslStream _stream;

        public EndPoint RemoteEndPoint => _baseConnection.RemoteEndPoint;
        public EndPoint LocalEndPoint => _baseConnection.LocalEndPoint;
        public IConnectionProperties ConnectionProperties => this;
        public Stream Stream => _stream;

        public SslConnection(IConnection baseConnection, SslStream stream)
        {
            _baseConnection = baseConnection;
            _stream = stream;
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            await _baseConnection.DisposeAsync().ConfigureAwait(false);
        }

        public void CompleteRead()
        {
            _baseConnection.CompleteRead();
        }

        public void CompleteWrite()
        {
            _baseConnection.CompleteWrite();
        }

        bool IConnectionProperties.TryGet(Type propertyType, out object property)
        {
            if (propertyType == typeof(ISslConnectionProperties))
            {
                property = _stream;
                return true;
            }

            return _baseConnection.ConnectionProperties.TryGet(propertyType, out property);
        }
    }
}
