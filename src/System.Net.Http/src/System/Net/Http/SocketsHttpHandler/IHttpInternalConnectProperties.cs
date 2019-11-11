using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal interface IHttpInternalConnectProperties : IHttpConnectProperties
    {
        HttpConnectionPool Pool { get; }
    }
}
