using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public interface IConnection : IConnectionStream
    {
        EndPoint RemoteEndPoint { get; }
        EndPoint LocalEndPoint { get; }
    }
}
