using Microsoft.AspNetCore.Http;

namespace OdataToEntity.AspServer
{
    public sealed class OeHttpRequestHeaders : OeRequestHeaders
    {
        private readonly HttpResponse _response;

        public OeHttpRequestHeaders(OeRequestHeaders headers, HttpResponse response)
            : base(headers.MimeType, headers.MetadataLevel, headers.Streaming, headers.Charset)
        {
            _response = response;
            _response.ContentType = base.ContentType;
        }

        public override string ResponseContentType
        {
            get => _response.ContentType;
            set => _response.ContentType = value;
        }
    }
}
