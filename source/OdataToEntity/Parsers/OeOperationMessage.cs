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
            public async ValueTask<(IEdmEntitySet entitySet, ODataResource resource)> CreateEntryAsync(IODataRequestMessage requestMessage)
            {
                if (requestMessage.Method == ODataConstants.MethodDelete)
                    return ReadEntityFromUrl(requestMessage.Url);

                String contentType = requestMessage.GetHeader(ODataConstants.ContentTypeHeader) ?? OeRequestHeaders.JsonDefault.ContentType;
                using (Stream stream = requestMessage.GetStream())
                    return await ReadEntityFromStreamAsync(stream, requestMessage.Url, contentType).ConfigureAwait(false);
            }
            private async ValueTask<(IEdmEntitySet entitySet, ODataResource resource)> ReadEntityFromStreamAsync(Stream content, Uri requestUrl, String contentType)
            {
                ODataUri odataUri = OeParser.ParseUri(_edmModel, _baseUri, requestUrl);
                IEdmEntitySet entitySet = ((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet;
                _edmEntityType = entitySet.EntityType();
                IEdmModel edmModel = _edmModel.GetEdmModel(entitySet);

                ODataResource? resource = null;
                IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(content, contentType, _serviceProvider);
                var settings = new ODataMessageReaderSettings
                {
                    ClientCustomTypeResolver = ClientCustomTypeResolver,
                    EnableMessageStreamDisposal = false
                };
                using (var messageReader = new ODataMessageReader(requestMessage, settings, edmModel))
                {
                    ODataReader reader = await messageReader.CreateODataResourceReaderAsync(entitySet, entitySet.EntityType()).ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        if (reader.State == ODataReaderState.ResourceEnd)
                            resource = (ODataResource)reader.Item;
                    if (resource == null)
                        throw new InvalidOperationException("operation not contain entry");
                }

                return (entitySet, resource);
            }
            private (IEdmEntitySet entitySet, ODataResource resource) ReadEntityFromUrl(Uri requestUrl)
            {
                ODataPath path = OeParser.ParsePath(_edmModel, _baseUri, requestUrl);
                var keySegment = (KeySegment)path.LastSegment;
                var entitySet = (IEdmEntitySet)keySegment.NavigationSource;

                var properties = new List<ODataProperty>();
                foreach (var key in keySegment.Keys)
                    properties.Add(new ODataProperty() { Name = key.Key, Value = key.Value });
                var resource = new ODataResource() { Properties = properties };

                return (entitySet, resource);
            }
        }

        private OeOperationMessage(IEdmEntitySet entitySet, ODataResource entry, IODataRequestMessage requestMessage, string contentId)
        {
            ContentId = contentId;
            ContentType = requestMessage.GetHeader(ODataConstants.ContentTypeHeader) ?? OeRequestHeaders.JsonDefault.ContentType;
            Method = requestMessage.Method;
            RequestUrl = requestMessage.Url;
            EntitySet = entitySet;
            Entry = entry;
        }

        public static async ValueTask<OeOperationMessage> CreateAsync(IEdmModel edmModel, Uri baseUri, ODataBatchReader reader, IServiceProvider? serviceProvider)
        {
            ODataBatchOperationRequestMessage batchRequest = await reader.CreateOperationRequestMessageAsync();
            var (entitySet, resource) = await (new ResourceFactory(edmModel, baseUri, serviceProvider)).CreateEntryAsync(batchRequest).ConfigureAwait(false);
            return new OeOperationMessage(entitySet, resource, batchRequest, batchRequest.ContentId);
        }
        public static async ValueTask<OeOperationMessage> CreateAsync(IEdmModel edmModel, Uri baseUri, IODataRequestMessage requestMessage, IServiceProvider? serviceProvider)
        {
            var (entitySet, resource) = await (new ResourceFactory(edmModel, baseUri, serviceProvider)).CreateEntryAsync(requestMessage).ConfigureAwait(false);
            return new OeOperationMessage(entitySet, resource, requestMessage, "");
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
