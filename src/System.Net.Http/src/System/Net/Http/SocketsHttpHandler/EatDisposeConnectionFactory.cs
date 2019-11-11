using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Connections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal sealed class EatDisposeConnectionFactory : IConnectionFactory
    {
        private readonly IConnectionFactory _baseFactory;

        public EatDisposeConnectionFactory(IConnectionFactory baseFactory)
        {
            _baseFactory = baseFactory;
        }

        public ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default)
        {
            return _baseFactory.ConnectAsync(endPoint, options, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            // this is used to wrap a user-specified connection factory.
            // we don't dispose their factory -- they need to handle outside of HttpClient.
            return default;
        }
    }
}
