using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace OdataToEntity.Parsers
{
    public sealed class OeOperationMessage
    {
        private readonly String _contentId;
        private readonly String _contentType;
        private readonly String _method;
        private readonly Uri _requestUrl;

        private OeOperationMessage(ODataBatchOperationRequestMessage batchRequest)
        {
            _contentId = batchRequest.ContentId;
            _contentType = batchRequest.GetHeader(ODataConstants.ContentTypeHeader);
            _method = batchRequest.Method;
            _requestUrl = batchRequest.Url;
        }

        public static OeOperationMessage Create(IEdmModel edmModel, Uri baseUri, ODataBatchReader reader)
        {
            ODataBatchOperationRequestMessage batchRequest = reader.CreateOperationRequestMessage();
            var operation = new OeOperationMessage(batchRequest);

            if (batchRequest.Method == ODataConstants.MethodDelete)
                operation.EntityItem = operation.ReadEntityFromUrl(edmModel, baseUri);
            else
            {
                using (Stream stream = batchRequest.GetStream())
                    operation.EntityItem = operation.ReadEntityFromStream(edmModel, baseUri, stream);
            }
            return operation;
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
        private OeEntityItem ReadEntityFromStream(IEdmModel edmModel, Uri baseUri, Stream content)
        {
            var parser = new ODataUriParser(edmModel, baseUri, RequestUrl);
            IEdmEntityTypeReference entityTypeRef = GetEdmEntityTypeRef(parser.ParsePath(), out IEdmEntitySet entitySet);
            var entityType = (IEdmEntityType)entityTypeRef.Definition;

            ODataResource entry = null;
            IODataRequestMessage requestMessage = new OeInMemoryMessage(content, ContentType);
            var settings = new ODataMessageReaderSettings { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, edmModel))
            {
                ODataReader reader = messageReader.CreateODataResourceReader(entitySet, entityType);

                while (reader.Read())
                    if (reader.State == ODataReaderState.ResourceEnd)
                        entry = (ODataResource)reader.Item;
                if (entry == null)
                    throw new InvalidOperationException("operation not contain entry");
            }

            return new OeEntityItem(entitySet, entityType, entry);
        }
        private OeEntityItem ReadEntityFromUrl(IEdmModel edmModel, Uri baseUri)
        {
            var parser = new ODataUriParser(edmModel, baseUri, RequestUrl);

            ODataPath path = parser.ParsePath();
            var keySegment = (KeySegment)path.LastSegment;
            var entityType = (IEdmEntityType)keySegment.EdmType;
            var entitySet = (IEdmEntitySet)keySegment.NavigationSource;

            var properties = new List<ODataProperty>(1);
            foreach (var key in keySegment.Keys)
                properties.Add(new ODataProperty() { Name = key.Key, Value = key.Value });
            var entry = new ODataResource() { Properties = properties };

            return new OeEntityItem(entitySet, entityType, entry);
        }

        public String ContentId => _contentId;
        public String ContentType => _contentType;
        public String Method => _method;
        public Uri RequestUrl => _requestUrl;
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
        public OeEntityItem EntityItem
        {
            get;
            private set;
        }
    }
}
