// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Connections;

namespace System.Net.Http
{
    internal abstract class HttpContentStream : HttpBaseStream, IConnection
    {
        protected HttpConnection _connection;

        public HttpContentStream(HttpConnection connection)
        {
            _connection = connection;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }

            base.Dispose(disposing);
        }

        protected HttpConnection GetConnectionOrThrow()
        {
            return _connection ??
                // This should only ever happen if the user-code that was handed this instance disposed of
                // it, which is misuse, or held onto it and tried to use it later after we've disposed of it,
                // which is also misuse.
                ThrowObjectDisposedException();
        }

        private HttpConnection ThrowObjectDisposedException() => throw new ObjectDisposedException(GetType().Name);

        // Forward all of the IConnection bits to the internal connection.
        public EndPoint LocalEndPoint => GetConnectionOrThrow()._connection.LocalEndPoint;
        public EndPoint RemoteEndPoint => GetConnectionOrThrow()._connection.RemoteEndPoint;
        public IConnectionProperties ConnectionProperties => GetConnectionOrThrow()._connection.ConnectionProperties;
        public Stream Stream => this;
        public void ShutdownReads() => GetConnectionOrThrow()._connection.ShutdownReads();
        public void ShutdownWrites() => GetConnectionOrThrow()._connection.ShutdownWrites();
    }
}
