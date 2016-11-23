using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.WcfService
{
    public class OdataWcfService : IOdataWcf
    {
        private readonly static Uri _baseUri = new Uri("http://dummy");
        private readonly OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OdataWcfService(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public async Task<Stream> Get(string query, String acceptHeader)
        {
            OeRequestHeaders headers = OeRequestHeaders.Parse(acceptHeader);
            var parser = new OeParser(_baseUri, DataAdapter, _edmModel);

            var uri = new Uri(_baseUri, new Uri(query, UriKind.Relative));
            var responseStream = new MemoryStream();
            await parser.ExecuteQueryAsync(uri, headers, responseStream, CancellationToken.None);
            responseStream.Position = 0;
            return responseStream;
        }
        public async Task<OdataWcfPostResponse> Post(OdataWcfPostRequest request)
        {
            var parser = new OeParser(_baseUri, _dataAdapter, _edmModel);
            var responseStream = new MemoryStream();
            await parser.ExecuteBatchAsync(request.RequestStream, responseStream, CancellationToken.None, request.ContentType);
            responseStream.Position = 0;
            return new OdataWcfPostResponse() { ResponseStream = responseStream };
        }

        public OeDataAdapter DataAdapter => _dataAdapter;
    }
}
