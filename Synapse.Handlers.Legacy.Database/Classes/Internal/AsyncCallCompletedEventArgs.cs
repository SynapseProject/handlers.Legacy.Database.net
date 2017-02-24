using System;

namespace Synapse.Internal.Api.Client
{
    public class AsyncCallCompletedEventArgs<T> : AsyncCallCompletedEventArgs
    {
        public AsyncCallCompletedEventArgs(T result)
            : base(result, typeof(T))
        {
            this.Result = result;
        }
        public AsyncCallCompletedEventArgs(T result, object userState)
            : base(result, typeof(T), userState)
        {
            this.Result = result;
        }
        public AsyncCallCompletedEventArgs(T result, object userState, Exception error, bool cancelled)
            : base(result, typeof(T), userState, error, cancelled)
        {
            this.Result = result;
        }

        public new T Result { get; private set; }
    }

    public class AsyncCallCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs
    {
        public AsyncCallCompletedEventArgs(object result, Type resultType)
            : this(result, resultType, null, null, false) { }
       
        public AsyncCallCompletedEventArgs(object result, Type resultType, object userState)
            : this(result, resultType, userState, null, false) { }

        public AsyncCallCompletedEventArgs(object result, Type resultType, object userState, Exception error, bool cancelled)
            : base(error, cancelled, userState)
        {
            this.Result = result;
            this.ResultType = resultType;
        }

        public object Result { get; private set; }
        public Type ResultType { get; private set; }
    }
}