using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public interface IConnectionStream : IAsyncDisposable
    {
        IConnectionProperties ConnectionProperties { get; }
        Stream Stream { get; }
        void CompleteRead();
        void CompleteWrite();
    }
}
