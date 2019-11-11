using System.Net.Connections;
using System.Net.Security;

namespace System.Net.Http
{
    internal sealed class HttpConnectProperties : IConnectionProperties, IHttpInternalConnectProperties
    {
        private readonly SslClientAuthenticationOptions _sslOptions;

        public HttpConnectProperties(HttpConnectionPool pool, HttpRequestMessage firstRequest, SslClientAuthenticationOptions sslOptions)
        {
            FirstRequest = firstRequest;
            Pool = pool;
            _sslOptions = sslOptions;
        }

        public bool TryGet(Type propertyType, out object property)
        {
            if (propertyType == typeof(IHttpInternalConnectProperties) || propertyType == typeof(IHttpConnectProperties))
            {
                property = this;
                return true;
            }

            if (propertyType == typeof(SslClientAuthenticationOptions) && _sslOptions != null)
            {
                property = _sslOptions;
                return true;
            }

            property = null;
            return false;
        }

        public HttpRequestMessage FirstRequest { get; }
        public Uri ProxyUri => Pool.ProxyUri;
        public HttpConnectionPool Pool { get; }
    }
}
