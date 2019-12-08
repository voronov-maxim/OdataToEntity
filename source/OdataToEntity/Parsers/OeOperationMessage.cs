using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OeOperationMessage
    {
        private struct ResourceFactory
        {
            private readonly Uri _baseUri;
            private IEdmEntityType? _edmEntityType;
            private readonly IEdmModel _edmModel;
            private readonly IServiceProvider? _serviceProvider;

            public ResourceFactory(IEdmModel edmModel, Uri baseUri, IServiceProvider? serviceProvider)
            {
                _edmModel = edmModel;
                _baseUri = baseUri;
                _serviceProvider = serviceProvider;
                _edmEntityType = null;
            }

            private IEdmType ClientCustomTypeResolver(IEdmType edmType, String name)
            {
                return edmType ?? (_edmEntityType ?? throw new InvalidOperationException(nameof(_edmEntityType) + " mist set in ReadEntityFromStream"));
            }
            public ODataResource CreateEntry(ODataBatchOperationRequestMessage batchRequest, out IEdmEntitySet entitSet)
            {
                if (batchRequest.Method == ODataConstants.MethodDelete)
                    return ReadEntityFromUrl(batchRequest.Url, out entitSet);

                String contentType = batchRequest.GetHeader(ODataConstants.ContentTypeHeader);
                using (Stream stream = batchRequest.GetStream())
                    return ReadEntityFromStream(stream, batchRequest.Url, contentType, out entitSet);
            }
            private ODataResource ReadEntityFromStream(Stream content, Uri requestUrl, String contentType, out IEdmEntitySet entitySet)
            {
                ODataUri odataUri = OeParser.ParseUri(_edmModel, _baseUri, requestUrl);
                entitySet = ((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet;
                _edmEntityType = entitySet.EntityType();
                IEdmModel edmModel = _edmModel.GetEdmModel(entitySet);

                ODataResource? entry = null;
                IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(content, contentType, _serviceProvider);
                var settings = new ODataMessageReaderSettings
                {
                    ClientCustomTypeResolver = ClientCustomTypeResolver,
                    EnableMessageStreamDisposal = false
                };
                using (var messageReader = new ODataMessageReader(requestMessage, settings, edmModel))
                {
                    ODataReader reader = messageReader.CreateODataResourceReader(entitySet, entitySet.EntityType());
                    while (reader.Read())
                        if (reader.State == ODataReaderState.ResourceEnd)
                            entry = (ODataResource)reader.Item;
                    if (entry == null)
                        throw new InvalidOperationException("operation not contain entry");
                }

                return entry;
            }
            private ODataResource ReadEntityFromUrl(Uri requestUrl, out IEdmEntitySet entitySet)
            {
                ODataPath path = OeParser.ParsePath(_edmModel, _baseUri, requestUrl);
                var keySegment = (KeySegment)path.LastSegment;
                entitySet = (IEdmEntitySet)keySegment.NavigationSource;

                var properties = new List<ODataProperty>();
                foreach (var key in keySegment.Keys)
                    properties.Add(new ODataProperty() { Name = key.Key, Value = key.Value });
                var entry = new ODataResource() { Properties = properties };

                return entry;
            }
        }

        private OeOperationMessage(ODataBatchOperationRequestMessage batchRequest, IEdmEntitySet entitySet, ODataResource entry)
        {
            ContentId = batchRequest.ContentId;
            ContentType = batchRequest.GetHeader(ODataConstants.ContentTypeHeader);
            Method = batchRequest.Method;
            RequestUrl = batchRequest.Url;
            EntitySet = entitySet;
            Entry = entry;
        }

        public static async ValueTask<OeOperationMessage> Create(IEdmModel edmModel, Uri baseUri, ODataBatchReader reader, IServiceProvider? serviceProvider)
        {
            ODataBatchOperationRequestMessage batchRequest = await reader.CreateOperationRequestMessageAsync();
            ODataResource entry = new ResourceFactory(edmModel, baseUri, serviceProvider).CreateEntry(batchRequest, out IEdmEntitySet entitSet);
            return new OeOperationMessage(batchRequest, entitSet, entry);
        }

        public String ContentId { get; }
        public String ContentType { get; }
        public IEdmEntitySet EntitySet { get; }
        public ODataResource Entry { get; }
        public String Method { get; }
        public Uri RequestUrl { get; }
        public HttpStatusCode StatusCode
        {
            get
            {
                return Method switch
                {
                    ODataConstants.MethodDelete => HttpStatusCode.OK,
                    ODataConstants.MethodPatch => HttpStatusCode.NoContent,
                    ODataConstants.MethodPost => HttpStatusCode.Created,
                    _ => throw new NotSupportedException(Method),
                };
            }
        }
    }
}
