using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public interface IConnectionFactory : IAsyncDisposable
    {
        ValueTask<IConnection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default);
    }
}
