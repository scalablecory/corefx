using System.Diagnostics;

namespace System.Net.Http
{
    internal sealed class ProxyConnectException : Exception
    {
        public HttpResponseMessage Response { get; }

        public ProxyConnectException(HttpResponseMessage response)
        {
            Debug.Assert(response != null);
            Response = response;
        }
    }
}
