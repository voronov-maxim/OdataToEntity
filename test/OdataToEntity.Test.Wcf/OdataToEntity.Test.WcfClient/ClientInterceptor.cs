using Microsoft.OData.Client;
using Microsoft.OData.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Test.WcfClient
{
    public abstract class ClientInterceptor
    {
        public void AttachToContext(DataServiceContext context)
        {
            context.Configurations.RequestPipeline.OnMessageCreating += CreateRequestMessage;
        }
        private DataServiceClientRequestMessage CreateRequestMessage(DataServiceClientRequestMessageArgs e)
        {
            return new ClientRequestMessage(this, e);
        }
        protected internal abstract Task<Stream> OnGetResponse(HttpWebRequestMessage requestMessage, Stream requestStream);
    }

    internal sealed class ClientRequestMessage : HttpWebRequestMessage
    {
        private sealed class NonClosedStream : MemoryStream
        {
            public override void Close()
            {
            }
        }

        private readonly ClientInterceptor _interceptor;
        private Stream _requestStream;

        public ClientRequestMessage(ClientInterceptor interceptor, DataServiceClientRequestMessageArgs args)
            : base(args)
        {
            _interceptor = interceptor;
        }

        public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, Object state)
        {
            return GetAsyncResult<Stream>(callback, new NonClosedStream(), state);
        }
        public override Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            _requestStream = ((Task<Stream>)asyncResult).Result;
            return _requestStream;
        }
        public override IAsyncResult BeginGetResponse(AsyncCallback callback, Object state)
        {
            return GetResponse(callback, state);
        }
        public override IODataResponseMessage EndGetResponse(IAsyncResult asyncResult)
        {
            String contentType = base.GetHeader("Accept");

            if (_requestStream != null)
                _requestStream.Dispose();

            var responseStream = ((Task<Stream>)asyncResult).Result;
            var headers = new Dictionary<String, String>(1) { { ODataConstants.ContentTypeHeader, contentType } };
            return new HttpWebResponseMessage(headers, 200, () => responseStream);
        }
        private IAsyncResult GetAsyncResult<T>(AsyncCallback callback, T result, Object asyncState)
        {
            var task = Task.FromResult<T>(result);
            var tcs = new TaskCompletionSource<T>(asyncState);

            task.ContinueWith(t =>
            {
                tcs.TrySetResult(t.Result);
                callback(tcs.Task);
            });
            return tcs.Task;
        }
        private IAsyncResult GetResponse(AsyncCallback callback, Object asyncState)
        {
            Task<Stream> responseTask = _interceptor.OnGetResponse(this, _requestStream);
            var tcs = new TaskCompletionSource<Stream>(asyncState);
            responseTask.ContinueWith(t =>
            {
                tcs.TrySetResult(t.Result);
                callback(tcs.Task);
            });
            return tcs.Task;
        }
        public override Stream GetStream()
        {
            _requestStream = new NonClosedStream();
            return _requestStream;
        }
        public override IODataResponseMessage GetResponse()
        {
            String contentType = base.GetHeader(ODataConstants.ContentTypeHeader);
            if (contentType == null)
                contentType = base.GetHeader("Accept");

            Stream responseStream = _interceptor.OnGetResponse(this, _requestStream).GetAwaiter().GetResult();

            var headers = new Dictionary<String, String>(1) { { ODataConstants.ContentTypeHeader, contentType } };
            return new HttpWebResponseMessage(headers, 200, () => responseStream);
        }
    }
}
