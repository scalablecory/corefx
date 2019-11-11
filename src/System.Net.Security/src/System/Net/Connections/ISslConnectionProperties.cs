using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace System.Net.Connections
{
    [CLSCompliant(false)] // due to TlsCipherSuite.
    public interface ISslConnectionProperties
    {
        SslProtocols SslProtocol { get; }
        TlsCipherSuite NegotiatedCipherSuite { get; }
        CipherAlgorithmType CipherAlgorithm { get; }
        int CipherStrength { get; }
        HashAlgorithmType HashAlgorithm { get; }
        int HashStrength { get; }
        ExchangeAlgorithmType KeyExchangeAlgorithm { get; }
        int KeyExchangeStrength { get; }
        X509Certificate LocalCertificate { get; }
        X509Certificate RemoteCertificate { get; }
        SslApplicationProtocol NegotiatedApplicationProtocol { get; }
        TransportContext TransportContext { get; }
    }
}
