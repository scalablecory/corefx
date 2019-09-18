using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net
{
    public unsafe partial class HttpListener
    {
        private sealed class HttpSysSession
        {
            private const int DISPOSED_FLAG = 1;

            private readonly object _lock = new object();
            private readonly SafeHandle _requestQueueHandle;
            private ThreadPoolBoundHandle _requestQueueBoundHandle;
            private int _refCount = 0;

            public SafeHandle RequestQueueHandle => _requestQueueHandle;
            public ThreadPoolBoundHandle RequestQueueBoundHandle
            {
                get
                {
                    if (_requestQueueBoundHandle == null)
                    {
                        lock (_lock)
                        {
                            if (_requestQueueBoundHandle == null)
                            {
                                _requestQueueBoundHandle = ThreadPoolBoundHandle.BindHandle(_requestQueueHandle);
                                if (NetEventSource.IsEnabled) NetEventSource.Info($"ThreadPoolBoundHandle.BindHandle({_requestQueueHandle}) -> {_requestQueueBoundHandle}");
                            }
                        }
                    }

                    return _requestQueueBoundHandle;
                }
            }

            public HttpSysSession(SafeHandle requestQueueHandle)
            {
                _requestQueueHandle = requestQueueHandle;
            }

            public bool TryAddRef()
            {
                int refCount = Interlocked.Add(ref _refCount, 2);
                return (refCount & DISPOSED_FLAG) == 0;
            }

            public void RemoveRef()
            {
                int refCount = Interlocked.Add(ref _refCount, -2);

                if (refCount == 0 && Interlocked.CompareExchange(ref _refCount, DISPOSED_FLAG, 0) == 0)
                {
                    if (_requestQueueBoundHandle != null)
                    {
                        if (NetEventSource.IsEnabled) NetEventSource.Info($"Dispose ThreadPoolBoundHandle: {_requestQueueBoundHandle}");
                        _requestQueueBoundHandle.Dispose();
                    }
                    _requestQueueHandle.Dispose();
                }
            }
        }
    }
}
