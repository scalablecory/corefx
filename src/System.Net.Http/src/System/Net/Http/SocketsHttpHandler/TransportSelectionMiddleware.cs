using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Connections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal sealed class TransportSelectionMiddleware : IConnectionFactory
    {
        private readonly IConnectionFactory _baseFactory;

        public TransportSelectionMiddleware(IConnectionFactory baseFactory)
        {
            Debug.Assert(baseFactory != null);
            _baseFactory = baseFactory;
        }

        public ValueTask DisposeAsync() => _baseFactory.DisposeAsync();

        public ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            Debug.Assert(endPoint == null, $"{nameof(TransportSelectionMiddleware)} expects a null {endPoint}.");

            if (!options.TryGet(out IHttpInternalConnectProperties httpProperties))
            {
                Debug.Fail($"{nameof(TransportSelectionMiddleware)} requires the {nameof(IHttpInternalConnectProperties)} connection property.");
            }

            TimeSpan connectTimeout = httpProperties.Pool.Settings._connectTimeout;

            if (connectTimeout == Timeout.InfiniteTimeSpan)
            {
                return ConnectAsyncHelper(endPoint, options, httpProperties, cancellationToken);
            }
            else
            {
                return ConnectAsyncWithTimeout(endPoint, options, httpProperties, connectTimeout, cancellationToken);
            }
        }

        private async ValueTask<IConnection> ConnectAsyncWithTimeout(EndPoint endPoint, IConnectionProperties options, IHttpInternalConnectProperties httpProperties, TimeSpan connectTimeout, CancellationToken cancellationToken)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, default);
            cts.CancelAfter(connectTimeout);

            try
            {
                return await ConnectAsyncHelper(endPoint, options, httpProperties, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cts.Token && cancellationToken.IsCancellationRequested)
            {
                // There is a race condition here in that our timeout may also have cancelled, but give preference to the passed in cancellation token.
                throw CancellationHelper.CreateOperationCanceledException(ex, cancellationToken);
            }
        }

        private ValueTask<IConnection> ConnectAsyncHelper(EndPoint endPoint, IConnectionProperties options, IHttpInternalConnectProperties httpProperties, CancellationToken cancellationToken)
        {
            switch (httpProperties.Pool.Kind)
            {
                case HttpConnectionKind.Http:
                case HttpConnectionKind.Https:
                case HttpConnectionKind.ProxyConnect:
                    return _baseFactory.ConnectAsync(new DnsEndPoint(httpProperties.Pool.Host, httpProperties.Pool.Port), options, cancellationToken);
                case HttpConnectionKind.Proxy:
                    return _baseFactory.ConnectAsync(new DnsEndPoint(httpProperties.Pool.ProxyUri.IdnHost, httpProperties.Pool.ProxyUri.Port), options, cancellationToken);
                case HttpConnectionKind.ProxyTunnel:
                case HttpConnectionKind.SslProxyTunnel:
                    return EstablishProxyTunnel(httpProperties, cancellationToken);
                default:
                    Debug.Fail($"{nameof(HttpConnectionPool)}.{nameof(HttpConnectionPool.Kind)} has unknown value '{httpProperties.Pool.Kind}'.");
                    return default;
            }
        }

        private async ValueTask<IConnection> EstablishProxyTunnel(IHttpInternalConnectProperties httpProperties, CancellationToken cancellationToken)
        {
            // Send a CONNECT request to the proxy server to establish a tunnel.
            HttpRequestMessage tunnelRequest = new HttpRequestMessage(HttpMethod.Connect, httpProperties.ProxyUri);
            tunnelRequest.Headers.Host = $"{httpProperties.Pool.Host}:{httpProperties.Pool.Port}";

            if (httpProperties.FirstRequest.Headers != null && httpProperties.FirstRequest.Headers.TryGetValues(HttpKnownHeaderNames.UserAgent, out IEnumerable<string> values) == true)
            {
                tunnelRequest.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.UserAgent, values);
            }

            HttpResponseMessage tunnelResponse = await httpProperties.Pool.PoolManager.SendProxyConnectAsync(tunnelRequest, httpProperties.ProxyUri, cancellationToken).ConfigureAwait(false);

            if (tunnelResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new ProxyConnectException(tunnelResponse);
            }

            Stream stream = await tunnelResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            IConnection connection = stream as HttpContentStream;

            return new ProxyConnectConnection(connection);
        }

        private sealed class ProxyConnectConnection : IConnection, IConnectionProperties
        {
            private readonly IConnection _responseConnection;

            public EndPoint LocalEndPoint => _responseConnection?.LocalEndPoint;
            public EndPoint RemoteEndPoint => _responseConnection?.RemoteEndPoint;
            public IConnectionProperties ConnectionProperties => this;
            public Stream Stream => _responseConnection?.Stream ?? EmptyReadStream.Instance;

            public ProxyConnectConnection(IConnection connection)
            {
                _responseConnection = connection;
            }

            public void ShutdownReads() => _responseConnection?.ShutdownReads();
            public void ShutdownWrites() => _responseConnection?.ShutdownWrites();
            public ValueTask DisposeAsync() => _responseConnection?.DisposeAsync() ?? default;

            public bool TryGet(Type propertyType, out object property)
            {
                if (propertyType == typeof(ISslConnectionProperties))
                {
                    // The TLS connection on a proxy is hidden here: an encrypted proxy
                    // does not mean the overall request is encrypted end-to-end. It is
                    // expected that HttpsConnectionMiddleware will add in the relevant
                    // TLS wrapper for the end-to-end connection.
                    property = null;
                    return false;
                }

                if (_responseConnection != null)
                {
                    return _responseConnection.ConnectionProperties.TryGet(propertyType, out property);
                }

                property = null;
                return false;
            }
        }
    }
}
