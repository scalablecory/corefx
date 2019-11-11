using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace System.Net.Connections
{
    internal sealed class NetworkStreamWithSocket : NetworkStream
    {
        public new Socket Socket => base.Socket;
        public NetworkStreamWithSocket(Socket socket) : base(socket, true)
        {
        }
    }
}
