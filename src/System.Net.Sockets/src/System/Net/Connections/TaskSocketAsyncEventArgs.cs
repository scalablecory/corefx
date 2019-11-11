using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Connections
{
    internal sealed class TaskSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<int> _valueTaskSource;

        public void ResetTask() => _valueTaskSource.Reset();
        public ValueTask Task => new ValueTask(this, _valueTaskSource.Version);

        public void GetResult(short token) => _valueTaskSource.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _valueTaskSource.GetStatus(token);
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => _valueTaskSource.OnCompleted(continuation, state, token, flags);

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            _valueTaskSource.SetResult(0);
        }
    }
}
