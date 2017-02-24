using System;
using System.Net;

namespace Synapse.Internal.Api.Client
{
    public class RequestData<T> : RequestData
    {
        public RequestData() { }
        public RequestData(Uri url, object state) : base(url, state, typeof(T)) { }
        public RequestData(Uri url, object state, byte[] data) : base(url, state, data, typeof(T)) { }
        public RequestData(Uri url, object state, byte[] data, string method) : base(url, state, data, typeof(T), method) { }
        public RequestData(HttpWebRequest request, object state) : base(request, state, typeof(T)) { }
        public new T Result { get { return (T)base.Result; } set { base.Result = value; } }
    }

    public class RequestData
    {
        public RequestData() { }
        public RequestData(Uri url, object state, Type resultType) : this(null, url, state, null, resultType) { }
        public RequestData(Uri url, object state, byte[] data, Type resultType) : this(null, url, state, data, resultType) { }
        public RequestData(Uri url, object state, byte[] data, Type resultType, string method) : this(null, url, state, data, resultType, method) { }
        public RequestData(HttpWebRequest request, object state, Type resultType) : this(request, request.RequestUri, state, new byte[0], resultType, request.Method) { }
        private RequestData(HttpWebRequest request, Uri url, object state, byte[] data, Type resultType, string method = HttpMethod.Get)
        {
            if (resultType == null) throw new ArgumentNullException("resultType");

            this.Request = request;
            this.Url = url;
            this.Method = method;
            this.State = state;
            this.Data = data;
            this.ResultType = resultType;
        }

        public Uri Url { get; set; }
        public HttpWebRequest Request { get; protected set; }
        public string Method { get; protected set; }
        public Object State { get; protected set; }
        public byte[] Data { get; protected set; }
        public object Result { get; set; }
        public Type ResultType { get; set; }

    }
}