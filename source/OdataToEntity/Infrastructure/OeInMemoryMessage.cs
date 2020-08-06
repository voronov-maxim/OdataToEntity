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
        private readonly String? _httpMethod;
        private readonly IServiceProvider? _serviceProvider;
        private readonly Stream _stream;
        private readonly Uri? _url;

        public OeInMemoryMessage(Stream stream, String? contentType) : this(stream, contentType, null)
        {
        }
        public OeInMemoryMessage(Stream stream, String? contentType, IServiceProvider? serviceProvider): this(stream, contentType, null, null, serviceProvider)
        {
        }
        public OeInMemoryMessage(Stream stream, String? contentType, Uri? url, String? httpMethod, IServiceProvider? serviceProvider)
        {
            _stream = stream;
            _headers = new Dictionary<String, String>(1);
            if (contentType != null)
                _headers.Add(ODataConstants.ContentTypeHeader, contentType);
            _url = url;
            _httpMethod = httpMethod;
            _serviceProvider = serviceProvider;
        }

        public String? GetHeader(String headerName)
        {
            _headers.TryGetValue(headerName, out String? result);
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
            get => _httpMethod ?? throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public int StatusCode
        {
            get;
            set;
        }
        public Uri Url
        {
            get => _url ?? throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        IServiceProvider? IContainerProvider.Container => _serviceProvider;
    }
}
