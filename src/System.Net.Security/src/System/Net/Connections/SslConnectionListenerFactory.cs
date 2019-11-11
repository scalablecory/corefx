using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Connections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Security;

namespace System.Net.Connections
{
    public sealed class SslConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly IConnectionListenerFactory _baseFactory;
        private readonly bool _skipIfAlreadyWrapped;

        public SslConnectionListenerFactory(IConnectionListenerFactory baseFactory, bool skipIfTls = false)
        {
            _baseFactory = baseFactory ?? throw new ArgumentNullException(nameof(baseFactory));
            _skipIfAlreadyWrapped = skipIfTls;
        }

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (!options.TryGet<SslServerAuthenticationOptions>(out SslServerAuthenticationOptions sslOptions))
            {
                throw new ArgumentException($"{nameof(options)} must contain a {nameof(SslServerAuthenticationOptions)}.");
            }

            IConnectionListener listener = await _baseFactory.BindAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new SslListener(listener, sslOptions, _skipIfAlreadyWrapped);
        }

        public ValueTask DisposeAsync()
        {
            return _baseFactory.DisposeAsync();
        }

        private sealed class SslListener : IConnectionListener
        {
            private readonly IConnectionListener _baseListener;
            private readonly SslServerAuthenticationOptions _options;
            private readonly bool _skipIfAlreadyWrapped;

            public SslListener(IConnectionListener baseListener, SslServerAuthenticationOptions options, bool skipIfAlreadyWrapped)
            {
                _baseListener = baseListener;
                _options = options;
                _skipIfAlreadyWrapped = skipIfAlreadyWrapped;
            }

            public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default)
            {
                IConnection con = await _baseListener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                SslStream sslStream = null;

                try
                {
                    if (_skipIfAlreadyWrapped && con.ConnectionProperties.TryGet(out ISslConnectionProperties ssl))
                    {
                        return con;
                    }

                    sslStream = new SslStream(con.Stream);
                    await sslStream.AuthenticateAsServerAsync(_options, cancellationToken).ConfigureAwait(false);

                    var ret = new SslConnection(con, sslStream);
                    sslStream = null;
                    return ret;
                }
                catch
                {
                    if (sslStream != null)
                    {
                        await sslStream.DisposeAsync().ConfigureAwait(false);
                    }
                    await con.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }

            public ValueTask DisposeAsync()
            {
                return _baseListener.DisposeAsync();
            }
        }
    }
}
