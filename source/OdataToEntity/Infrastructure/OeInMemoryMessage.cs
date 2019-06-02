using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.IO;

namespace OdataToEntity.Infrastructure
{
    public sealed class OeInMemoryMessage : IODataRequestMessage, IODataResponseMessage, IContainerProvider
    {
        private readonly Dictionary<String, String> _headers;
        private readonly IServiceProvider _serviceProvider;
        private readonly Stream _stream;

        public OeInMemoryMessage(Stream stream, String contentType) : this(stream, contentType, null)
        {
        }
        public OeInMemoryMessage(Stream stream, String contentType, IServiceProvider serviceProvider)
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
        public Stream GetStream() => _stream;
        public void SetHeader(String headerName, String headerValue) => _headers[headerName] = headerValue;

        public IEnumerable<KeyValuePair<String, String>> Headers => _headers;
        public String Method
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
        public int StatusCode
        {
            get;
            set;
        }
        public Uri Url
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        IServiceProvider IContainerProvider.Container => _serviceProvider;
    }
}
