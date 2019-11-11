// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace System.Net.Http
{
    /// <summary>
    /// Helper type used by HttpClientHandler when wrapping SocketsHttpHandler to map its
    /// certificate validation callback to the one used by SslStream.
    /// </summary>
    internal sealed class CertificateCallbackMapper
    {
        public readonly Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> FromHttpClientHandler;
        public readonly RemoteCertificateValidationCallback ForSocketsHttpHandler;

        public CertificateCallbackMapper(Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> fromHttpClientHandler)
        {
            FromHttpClientHandler = fromHttpClientHandler;
            ForSocketsHttpHandler = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                FromHttpClientHandler(sender as HttpRequestMessage, certificate as X509Certificate2, chain, sslPolicyErrors);
        }
    }
}
