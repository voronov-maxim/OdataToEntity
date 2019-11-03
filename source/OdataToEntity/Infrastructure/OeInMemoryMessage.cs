using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Infrastructure
{
    public sealed class OeInMemoryMessage : IODataRequestMessage, IODataRequestMessageAsync, IODataResponseMessage, IODataResponseMessageAsync, IContainerProvider
    {
        private readonly Dictionary<String, String> _headers;
        private readonly IServiceProvider? _serviceProvider;
        private readonly Stream _stream;

        public OeInMemoryMessage(Stream stream, String? contentType) : this(stream, contentType, null)
        {
        }
        public OeInMemoryMessage(Stream stream, String? contentType, IServiceProvider? serviceProvider)
        {
            _stream = stream;
            _headers = new Dictionary<String, String>(1);
            if (contentType != null)
                _headers.Add(ODataConstants.ContentTypeHeader, contentType);
            _serviceProvider = serviceProvider;
        }

        public String GetHeader(String headerName)
        {
            _headers.TryGetValue(headerName, out String result);
            return result;
        }
        public Stream GetStream()
        {
            return _stream;
        }
        public void SetHeader(String headerName, String headerValue) => _headers[headerName] = headerValue;

        public Task<Stream> GetStreamAsync()
        {
            return Task.FromResult(_stream);
        }

        public IEnumerable<KeyValuePair<String, String>> Headers => _headers;
        public String Method
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public int StatusCode
        {
            get;
            set;
        }
        public Uri Url
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        IServiceProvider? IContainerProvider.Container => _serviceProvider;
    }
}
