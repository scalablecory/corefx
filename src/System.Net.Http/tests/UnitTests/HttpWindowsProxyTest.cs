// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.WinHttpHandlerUnitTests;
using Microsoft.DotNet.RemoteExecutor;
using Xunit;
using Xunit.Abstractions;

namespace System.Net.Http.Tests
{
    public class HttpWindowsProxyTest
    {
        private readonly ITestOutputHelper _output;
        private const string FakeProxyString = "http://proxy.contoso.com";
        private const string insecureProxyUri = "http://proxy.insecure.com";
        private const string secureProxyUri = "http://proxy.secure.com";
        private const string secureAndInsecureProxyUri = "http://proxy.secure-and-insecure.com";
        private const string fooHttp = "http://foo.com";
        private const string fooHttps = "https://foo.com";
        private const string fooWs = "ws://foo.com";
        private const string fooWss = "wss://foo.com";

        public HttpWindowsProxyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(ProxyParsingData))]
        public void HttpProxy_WindowsProxy_Manual_Loaded(string rawProxyString, string rawInsecureUri, string rawSecureUri)
        {
            RemoteExecutor.Invoke((proxyString, insecureProxy, secureProxy) =>
            {
                FakeRegistry.Reset();

                Assert.False(HttpWindowsProxy.TryCreate(out IWebProxy p));

                FakeRegistry.WinInetProxySettings.Proxy = proxyString;
                WinInetProxyHelper proxyHelper = new WinInetProxyHelper();
                Assert.Null(proxyHelper.AutoConfigUrl);
                Assert.Equal(proxyString, proxyHelper.Proxy);
                Assert.False(proxyHelper.AutoSettingsUsed);
                Assert.True(proxyHelper.ManualSettingsUsed);

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);

                Assert.Equal(!string.IsNullOrEmpty(insecureProxy) ? new Uri(insecureProxy) : null, p.GetProxy(new Uri(fooHttp)));
                Assert.Equal(!string.IsNullOrEmpty(secureProxy) ? new Uri(secureProxy) : null, p.GetProxy(new Uri(fooHttps)));
                Assert.Equal(!string.IsNullOrEmpty(insecureProxy) ? new Uri(insecureProxy) : null, p.GetProxy(new Uri(fooWs)));
                Assert.Equal(!string.IsNullOrEmpty(secureProxy) ? new Uri(secureProxy) : null, p.GetProxy(new Uri(fooWss)));
                return RemoteExecutor.SuccessExitCode;
            }, rawProxyString, rawInsecureUri ?? string.Empty, rawSecureUri ?? string.Empty).Dispose();
        }

        [Theory]
        [MemberData(nameof(ProxyParsingData))]
        public void HttpProxy_WindowsProxy_PAC_Loaded(string rawProxyString, string rawInsecureUri, string rawSecureUri)
        {
            RemoteExecutor.Invoke((proxyString, insecureProxy, secureProxy) =>
            {
                TestControl.ResetAll();

                Assert.False(HttpWindowsProxy.TryCreate(out IWebProxy p));

                FakeRegistry.WinInetProxySettings.AutoConfigUrl = "http://127.0.0.1/proxy.pac";
                WinInetProxyHelper proxyHelper = new WinInetProxyHelper();
                Assert.Null(proxyHelper.Proxy);
                Assert.Equal(FakeRegistry.WinInetProxySettings.AutoConfigUrl, proxyHelper.AutoConfigUrl);
                Assert.False(proxyHelper.ManualSettingsUsed);
                Assert.True(proxyHelper.AutoSettingsUsed);

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);

                // With a HttpWindowsProxy created configured to use auto-config, now set Proxy so when it
                // attempts to resolve a proxy, it resolves our string.
                FakeRegistry.WinInetProxySettings.Proxy = proxyString;
                proxyHelper = new WinInetProxyHelper();
                Assert.Equal(proxyString, proxyHelper.Proxy);

                Assert.Equal(!string.IsNullOrEmpty(insecureProxy) ? new Uri(insecureProxy) : null, p.GetProxy(new Uri(fooHttp)));
                Assert.Equal(!string.IsNullOrEmpty(secureProxy) ? new Uri(secureProxy) : null, p.GetProxy(new Uri(fooHttps)));
                Assert.Equal(!string.IsNullOrEmpty(insecureProxy) ? new Uri(insecureProxy) : null, p.GetProxy(new Uri(fooWs)));
                Assert.Equal(!string.IsNullOrEmpty(secureProxy) ? new Uri(secureProxy) : null, p.GetProxy(new Uri(fooWss)));
                return RemoteExecutor.SuccessExitCode;
            }, rawProxyString, rawInsecureUri ?? string.Empty, rawSecureUri ?? string.Empty).Dispose();
        }

        public static TheoryData<string, string, string> ProxyParsingData =>
            new TheoryData<string, string, string>
            {
                { "http://proxy.insecure.com", insecureProxyUri, null },
                { "http=http://proxy.insecure.com", insecureProxyUri, null },
                { "http=proxy.insecure.com", insecureProxyUri, null },
                { "http://proxy.insecure.com http://proxy.wrong.com", insecureProxyUri, null },
                { "https=proxy.secure.com http=proxy.insecure.com", insecureProxyUri, secureProxyUri },
                { "https://proxy.secure.com\nhttp://proxy.insecure.com", insecureProxyUri, secureProxyUri },
                { "https=proxy.secure.com\nhttp=proxy.insecure.com", insecureProxyUri, secureProxyUri },
                { "https://proxy.secure.com;http://proxy.insecure.com", insecureProxyUri, secureProxyUri },
                { "https=proxy.secure.com;http=proxy.insecure.com", insecureProxyUri, secureProxyUri },
                { ";http=proxy.insecure.com;;", insecureProxyUri, null },
                { "    http=proxy.insecure.com    ", insecureProxyUri, null },
                { "http=proxy.insecure.com;http=proxy.wrong.com", insecureProxyUri, null },
                { "http=http://proxy.insecure.com", insecureProxyUri, null },
                { "https://proxy.secure.com", null, secureProxyUri },
                { "https=proxy.secure.com", null, secureProxyUri },
                { "https=https://proxy.secure.com", null, secureProxyUri },
                { "http=https://proxy.secure.com", null, secureProxyUri },
                { "https=http://proxy.insecure.com", insecureProxyUri, null },
                { "proxy.secure-and-insecure.com", secureAndInsecureProxyUri, secureAndInsecureProxyUri },
            };

        [Theory]
        [InlineData("localhost:1234", "http://localhost:1234/")]
        [InlineData("123.123.123.123", "http://123.123.123.123/")]
        public void HttpProxy_WindowsProxy_Loaded(string rawProxyString, string expectedUri)
        {
            RemoteExecutor.Invoke((proxyString, expectedString) =>
            {
                IWebProxy p;

                FakeRegistry.Reset();

                FakeRegistry.WinInetProxySettings.Proxy = proxyString;
                WinInetProxyHelper proxyHelper = new WinInetProxyHelper();

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);
                Assert.Equal(expectedString, p.GetProxy(new Uri(fooHttp)).ToString());
                Assert.Equal(expectedString, p.GetProxy(new Uri(fooHttps)).ToString());

                return RemoteExecutor.SuccessExitCode;
            }, rawProxyString, expectedUri).Dispose();
        }

        [Theory]
        [InlineData("http://localhost/", true)]
        [InlineData("http://127.0.0.1/", true)]
        [InlineData("http://128.0.0.1/", false)]
        [InlineData("http://[::1]/", true)]
        [InlineData("http://foo/", true)]
        [InlineData("http://www.foo.com/", true)]
        [InlineData("http://WWW.FOO.COM/", true)]
        [InlineData("http://foo.com/", false)]
        [InlineData("http://bar.com/", true)]
        [InlineData("http://BAR.COM/", true)]
        [InlineData("http://162.1.1.1/", true)]
        [InlineData("http://[2a01:5b40:0:248::52]/", false)]
        [InlineData("http://[2002::11]/", true)]
        [InlineData("http://[2607:f8b0:4005:80a::200e]/", true)]
        [InlineData("http://[2607:f8B0:4005:80A::200E]/", true)]
        [InlineData("http://b\u00e9b\u00e9.eu/", true)]
        [InlineData("http://www.b\u00e9b\u00e9.eu/", true)]
        public void HttpProxy_Local_Bypassed(string name, bool shouldBypass)
        {
            RemoteExecutor.Invoke((url, expected) =>
            {
                bool expectedResult = Boolean.Parse(expected);
                IWebProxy p;

                FakeRegistry.Reset();
                FakeRegistry.WinInetProxySettings.Proxy = insecureProxyUri;
                FakeRegistry.WinInetProxySettings.ProxyBypass = "23.23.86.44;*.foo.com;<local>;BAR.COM; ; 162*;[2002::11];[*:f8b0:4005:80a::200e]; http://www.xn--mnchhausen-9db.at;http://*.xn--bb-bjab.eu;http://xn--bb-bjab.eu;";

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);

                Uri u = new Uri(url);
                Assert.Equal(expectedResult, p.GetProxy(u) == null);

                return RemoteExecutor.SuccessExitCode;
           }, name, shouldBypass.ToString()).Dispose();
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData(" ", 0)]
        [InlineData(" ; ;  ", 0)]
        [InlineData("http://127.0.0.1/", 1)]
        [InlineData("[::]", 1)]
        public void HttpProxy_Local_Parsing(string bypass, int count)
        {
            RemoteExecutor.Invoke((bypassValue, expected) =>
            {
                int expectedCount = Convert.ToInt32(expected);
                IWebProxy p;

                FakeRegistry.Reset();
                FakeRegistry.WinInetProxySettings.Proxy = insecureProxyUri;
                FakeRegistry.WinInetProxySettings.ProxyBypass = bypassValue;

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);

                HttpWindowsProxy sp = p as HttpWindowsProxy;
                Assert.NotNull(sp);

                if (expectedCount > 0)
                {
                    Assert.Equal(expectedCount, sp.BypassList.Count);
                }
                else
                {
                    Assert.Null(sp.BypassList);
                }
                return RemoteExecutor.SuccessExitCode;
           }, bypass, count.ToString()).Dispose();
        }

        [Theory]
        [InlineData("http://")]
        [InlineData("http=")]
        [InlineData("http://;")]
        [InlineData("http=;")]
        [InlineData("  ;  ")]
        public void HttpProxy_InvalidWindowsProxy_Null(string rawProxyString)
        {
            RemoteExecutor.Invoke((proxyString) =>
            {
                IWebProxy p;

                FakeRegistry.Reset();
                Assert.False(HttpWindowsProxy.TryCreate(out p));

                FakeRegistry.WinInetProxySettings.Proxy = proxyString;
                WinInetProxyHelper proxyHelper = new WinInetProxyHelper();

                Assert.True(HttpWindowsProxy.TryCreate(out p));
                Assert.NotNull(p);

                Assert.Null(p.GetProxy(new Uri(fooHttp)));
                Assert.Null(p.GetProxy(new Uri(fooHttps)));
                Assert.Null(p.GetProxy(new Uri(fooWs)));
                Assert.Null(p.GetProxy(new Uri(fooWss)));
                return RemoteExecutor.SuccessExitCode;
            }, rawProxyString).Dispose();
        }

        [Theory]
        [MemberData(nameof(HttpProxy_Multi_Data))]
        public void HttpProxy_Multi_Success(bool manualConfig, string proxyConfig, string url, string expected)
        {
            RemoteExecutor.Invoke((manualConfigValue, proxyConfigValue, urlValue, expectedValue) =>
            {
                bool manual = bool.Parse(manualConfigValue);
                Uri requestUri = new Uri(urlValue);
                string[] expectedUris = expectedValue.Split(';');

                TestControl.ResetAll();

                if (manual)
                {
                    FakeRegistry.WinInetProxySettings.Proxy = proxyConfigValue;
                }
                else
                {
                    FakeRegistry.WinInetProxySettings.AutoConfigUrl = "http://dummy.com";
                }

                Assert.True(HttpWindowsProxy.TryCreate(out IWebProxy p));
                HttpWindowsProxy wp = Assert.IsType<HttpWindowsProxy>(p);

                if (!manual)
                {
                    // Now that HttpWindowsProxy has been constructed to use autoconfig,
                    // set Proxy which will be used by Fakes for all the per-URL calls.
                    FakeRegistry.WinInetProxySettings.Proxy = proxyConfigValue;
                }

                MultiProxy multi = wp.GetMultiProxy(requestUri);

                if (expectedValue.Length == 0)
                {
                    Assert.Null(multi);
                }
                else
                {
                    Assert.NotNull(multi);
                    Assert.Equal(expectedUris.Length, multi.ProxyCount);

                    MultiProxy.MultiProxyEnumerator e = multi.GetEnumerator();

                    for (int i = 0; i < expectedUris.Length; ++i)
                    {
                        // Both the current enumerator and the proxy globally should move to the next proxy.
                        Assert.True(e.MoveNext());
                        Assert.Equal(new Uri(expectedUris[i]), e.Current);
                        Assert.Equal(new Uri(expectedUris[i]), multi.GetNextProxy());
                        Assert.Equal(new Uri(expectedUris[i]), p.GetProxy(requestUri));
                    }

                    Assert.False(e.MoveNext());
                }

                return RemoteExecutor.SuccessExitCode;
            }, manualConfig.ToString(), proxyConfig, url, expected).Dispose();
        }

        public static IEnumerable<object[]> HttpProxy_Multi_Data()
        {
            for (int i = 0; i < 2; ++i)
            {
                yield return new object[] { i == 0, "http://proxy.com", "http://request.com", "http://proxy.com" };
                yield return new object[] { i == 0, "http://proxy.com https://secure-proxy.com", "http://request.com", "http://proxy.com" };
                yield return new object[] { i == 0, "http://proxy-a.com https://secure-proxy.com http://proxy-b.com", "http://request.com", "http://proxy-a.com;http://proxy-b.com" };
                yield return new object[] { i == 0, "http://proxy-a.com https://secure-proxy.com http://proxy-b.com", "https://request.com", "http://secure-proxy.com" };
                yield return new object[] { i == 0, "http://proxy-a.com https://secure-proxy-a.com http://proxy-b.com  https://secure-proxy-b.com  https://secure-proxy-c.com", "https://request.com", "http://secure-proxy-a.com;http://secure-proxy-b.com;http://secure-proxy-c.com" };
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HttpProxy_Multi_ConcurrentUse_Success(bool manualConfig)
        {
            const string MultiProxyConfig = "http://proxy-a.com http://proxy-b.com";

            RemoteExecutor.Invoke(manualValue =>
            {
                bool manual = bool.Parse(manualValue);

                Uri requestUri = new Uri("http://request.com");
                Uri firstProxy = new Uri("http://proxy-a.com");
                Uri secondProxy = new Uri("http://proxy-b.com");

                TestControl.ResetAll();

                if (manual)
                {
                    FakeRegistry.WinInetProxySettings.Proxy = MultiProxyConfig;
                }
                else
                {
                    FakeRegistry.WinInetProxySettings.AutoConfigUrl = "http://dummy.com";
                }

                Assert.True(HttpWindowsProxy.TryCreate(out IWebProxy p));
                HttpWindowsProxy wp = Assert.IsType<HttpWindowsProxy>(p);

                if (!manual)
                {
                    // Now that HttpWindowsProxy has been constructed to use autoconfig,
                    // set Proxy which will be used by Fakes for all the per-URL calls.
                    FakeRegistry.WinInetProxySettings.Proxy = MultiProxyConfig;
                }

                MultiProxy multiA = wp.GetMultiProxy(requestUri);
                MultiProxy multiB = wp.GetMultiProxy(requestUri);

                Assert.NotNull(multiA);
                Assert.Same(multiA, multiB); // The second request should give us a cached value.
                Assert.Equal(2, multiA.ProxyCount);

                MultiProxy.MultiProxyEnumerator enumA = multiA.GetEnumerator();
                MultiProxy.MultiProxyEnumerator enumB = multiB.GetEnumerator();

                // Assert first proxy is returned and no index is moved upon enumerating.
                Assert.True(enumA.MoveNext());
                Assert.True(enumB.MoveNext());
                Assert.Equal(firstProxy, enumA.Current);
                Assert.Equal(firstProxy, enumB.Current);
                Assert.Equal(firstProxy, multiA.GetNextProxy());
                Assert.Equal(firstProxy, p.GetProxy(requestUri));

                // Assert global index is moved upon enumerating.
                Assert.True(enumA.MoveNext());
                Assert.Equal(secondProxy, multiA.GetNextProxy());
                Assert.Equal(secondProxy, p.GetProxy(requestUri));

                // But, enumerators already created should not track the global index.
                // Additionally, the global index shouldn't move when moving enumB forward, because enumA already moved it.
                Assert.True(enumB.MoveNext());
                Assert.Equal(secondProxy, enumB.Current);
                Assert.Equal(secondProxy, multiA.GetNextProxy());
                Assert.Equal(secondProxy, p.GetProxy(requestUri));

                // When enumA returns false, the global proxy should be moved back to the original.
                Assert.False(enumA.MoveNext());
                Assert.Equal(firstProxy, multiA.GetNextProxy());
                Assert.Equal(firstProxy, p.GetProxy(requestUri));

                // When enumB returns false, the global proxy shouldn't change because enumA already changed it.
                Assert.False(enumB.MoveNext());
                Assert.Equal(firstProxy, multiA.GetNextProxy());
                Assert.Equal(firstProxy, p.GetProxy(requestUri));

               return RemoteExecutor.SuccessExitCode;
            }, manualConfig.ToString()).Dispose();
        }
    }
}
