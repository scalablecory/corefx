using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public interface IHttpConnectProperties
    {
        public HttpRequestMessage FirstRequest { get; }
        public Uri ProxyUri { get; }
    }
}
