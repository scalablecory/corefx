using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public class SslConnectionFactory : IConnectionFactory
    {
        private readonly IConnectionFactory _baseFactory;
        private readonly bool _skipIfAlreadyWrapped;

        protected IConnectionFactory BaseFactory => _baseFactory;

        public SslConnectionFactory(IConnectionFactory baseFactory, bool skipIfTls = false)
        {
            _baseFactory = baseFactory ?? throw new ArgumentNullException(nameof(baseFactory));
            _skipIfAlreadyWrapped = skipIfTls;
        }

        public virtual async ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            IConnection con = await _baseFactory.ConnectAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            SslStream sslStream = null;

            try
            {
                if (_skipIfAlreadyWrapped && con.ConnectionProperties.TryGet<ISslConnectionProperties>(out ISslConnectionProperties ssl))
                {
                    return con;
                }

                if (!options.TryGet<SslClientAuthenticationOptions>(out SslClientAuthenticationOptions sslOptions))
                {
                    throw new ArgumentException($"{nameof(options)} must contain a {nameof(SslClientAuthenticationOptions)}.");
                }

                sslStream = new SslStream(con.Stream);
                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);

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
            return _baseFactory.DisposeAsync();
        }
    }
}
