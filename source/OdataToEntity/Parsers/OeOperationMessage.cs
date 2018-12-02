using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace OdataToEntity.Parsers
{
    public readonly struct OeOperationMessage
    {
        private OeOperationMessage(ODataBatchOperationRequestMessage batchRequest, IEdmEntitySet entitySet, ODataResource entry)
        {
            ContentId = batchRequest.ContentId;
            ContentType = batchRequest.GetHeader(ODataConstants.ContentTypeHeader);
            Method = batchRequest.Method;
            RequestUrl = batchRequest.Url;
            EntitySet = entitySet;
            Entry = entry;
        }

        public static OeOperationMessage Create(IEdmModel edmModel, Uri baseUri, ODataBatchReader reader)
        {
            ODataBatchOperationRequestMessage batchRequest = reader.CreateOperationRequestMessage();
            ODataResource entry = CreateEntry(edmModel, baseUri, batchRequest, out IEdmEntitySet entitSet);
            return new OeOperationMessage(batchRequest, entitSet, entry);
        }
        private static ODataResource CreateEntry(IEdmModel edmModel, Uri baseUri, ODataBatchOperationRequestMessage batchRequest, out IEdmEntitySet entitSet)
        {
            if (batchRequest.Method == ODataConstants.MethodDelete)
                return ReadEntityFromUrl(edmModel, baseUri, batchRequest.Url, out entitSet);

            String contentType = batchRequest.GetHeader(ODataConstants.ContentTypeHeader);
            using (Stream stream = batchRequest.GetStream())
                return ReadEntityFromStream(edmModel, baseUri, stream, batchRequest.Url, contentType, out entitSet);
        }
        private static IEdmEntityTypeReference GetEdmEntityTypeRef(ODataPath odataPath, out IEdmEntitySet entitySet)
        {
            entitySet = null;
            foreach (ODataPathSegment segment in odataPath)
                if (segment is EntitySetSegment entitySegment)
                {
                    entitySet = entitySegment.EntitySet;
                    return (IEdmEntityTypeReference)((IEdmCollectionType)entitySegment.EdmType).ElementType;
                }
            throw new InvalidOperationException("not supported type ODataPath");
        }
        private static ODataResource ReadEntityFromStream(IEdmModel edmModel, Uri baseUri, Stream content, Uri requestUrl, String contentType, out IEdmEntitySet entitySet)
        {
            ODataUri odataUri = OeParser.ParseUri(edmModel, baseUri, requestUrl);
            IEdmEntityTypeReference entityTypeRef = GetEdmEntityTypeRef(odataUri.Path, out entitySet);
            edmModel = edmModel.GetEdmModel(entitySet);

            ODataResource entry = null;
            IODataRequestMessage requestMessage = new OeInMemoryMessage(content, contentType);
            var settings = new ODataMessageReaderSettings { EnableMessageStreamDisposal = false };
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
        private static ODataResource ReadEntityFromUrl(IEdmModel edmModel, Uri baseUri, Uri requestUrl, out IEdmEntitySet entitySet)
        {
            ODataPath path = OeParser.ParsePath(edmModel, baseUri, requestUrl);
            var keySegment = (KeySegment)path.LastSegment;
            entitySet = (IEdmEntitySet)keySegment.NavigationSource;

            var properties = new List<ODataProperty>();
            foreach (var key in keySegment.Keys)
                properties.Add(new ODataProperty() { Name = key.Key, Value = key.Value });
            var entry = new ODataResource() { Properties = properties };

            return entry;
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
                switch (Method)
                {
                    case ODataConstants.MethodDelete:
                        return HttpStatusCode.OK;
                    case ODataConstants.MethodPatch:
                        return HttpStatusCode.NoContent;
                    case ODataConstants.MethodPost:
                        return HttpStatusCode.Created;
                    default:
                        throw new NotImplementedException(Method);
                }
            }
        }
    }
}
