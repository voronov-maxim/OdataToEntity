using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.WcfService
{
    public class OdataWcfService : IOdataWcf
    {
        private readonly static Uri _baseUri = new Uri("http://dummy");
        private readonly IEdmModel _edmModel;

        public OdataWcfService(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            DataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public async Task<OdataWcfQuery> Get(OdataWcfQuery request)
        {
            OeRequestHeaders headers = OeRequestHeaders.Parse(request.ContentType, request.Prefer);
            headers.ResponseContentType = headers.ContentType;

            Query.OeModelBoundProvider modelBoundProvider = null;
            if (headers.MaxPageSize > 0)
            {
                var pageNextLinkModelBoundBuilder = new PageNextLinkModelBoundBuilder(_edmModel, false);
                modelBoundProvider = pageNextLinkModelBoundBuilder.BuildProvider(headers.MaxPageSize, false);
            }
            var parser = new OeParser(_baseUri, _edmModel, modelBoundProvider);

            String query = new StreamReader(request.Content).ReadToEnd();
            var uri = new Uri(_baseUri, new Uri(query, UriKind.Relative));
            var responseStream = new MemoryStream();

            await parser.ExecuteGetAsync(uri, headers, responseStream, CancellationToken.None);
            responseStream.Position = 0;
            return new OdataWcfQuery()
            {
                Content = responseStream,
                ContentType = headers.ResponseContentType
            };
        }
        public async Task<OdataWcfQuery> Post(OdataWcfQuery request)
        {
            var parser = new OeParser(_baseUri, _edmModel);
            var responseStream = new MemoryStream();
            await parser.ExecuteBatchAsync(request.Content, responseStream, request.ContentType, CancellationToken.None);
            responseStream.Position = 0;
            return new OdataWcfQuery()
            {
                Content = responseStream,
                ContentType = request.ContentType
            };
        }

        public OeDataAdapter DataAdapter { get; }
    }
}
