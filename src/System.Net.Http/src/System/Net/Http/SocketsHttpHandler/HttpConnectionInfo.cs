using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http
{
    /// <summary>
    /// Provides information related to an HTTP connection.
    /// </summary>
    /// <remarks>
    /// Used with <see cref="SocketsHttpHandler.ConnectCallback"/>.
    /// TODO: make this base class of HttpConnectionPool to avoid the allocation?
    /// </remarks>
    public class HttpConnectionInfo
    {
        /// <summary>
        /// The hostname the handler is connected to. If connecting through a proxy, this will be the proxy's hostname.
        /// </summary>
        public string Hostname { get; internal set; }

        /// <summary>
        /// The port the handler is connected to. If connecting through a proxy, this will be the proxy's port.
        /// </summary>
        public int Port { get; internal set; }

        /// <summary>
        /// The hostname of the URI requests are being made to.
        /// </summary>
        public string UriHostname { get; internal set; }

        /// <summary>
        /// The hostname of the URI requests are being made to.
        /// </summary>
        public int UriPort { get; internal set; }
    }
}
