using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public sealed class HttpsConnectionMiddleware : SslConnectionFactory
    {
        public HttpsConnectionMiddleware(IConnectionFactory baseFactory)
            : base(baseFactory, true)
        {
        }

        public override async ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            if (!options.TryGet(out IHttpInternalConnectProperties httpProperties))
            {
                throw new HttpRequestException($"{nameof(HttpsConnectionMiddleware)} requires the {nameof(IHttpInternalConnectProperties)} connection property.");
            }

            HttpConnectionKind kind = httpProperties.Pool.Kind;

            ValueTask<IConnection> task =
                (kind == HttpConnectionKind.Https || kind == HttpConnectionKind.SslProxyTunnel)
                ? base.ConnectAsync(endPoint, options, cancellationToken)
                : BaseFactory.ConnectAsync(endPoint, options, cancellationToken);

            return await task.ConfigureAwait(false);
        }
    }
}
