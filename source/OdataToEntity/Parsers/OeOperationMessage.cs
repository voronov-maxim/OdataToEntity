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

        public static OeOperationMessage Create(OeMessageContext context, ODataBatchReader reader)
        {
            ODataBatchOperationRequestMessage batchRequest = reader.CreateOperationRequestMessage();
            var operation = new OeOperationMessage(batchRequest);

            if (batchRequest.Method == ODataConstants.MethodDelete)
                operation.EntityItem = operation.ReadEntityFromUrl(context);
            else
            {
                using (Stream stream = batchRequest.GetStream())
                    operation.EntityItem = operation.ReadEntityFromStream(context, stream);
            }
            return operation;
        }
        private static IEdmEntityTypeReference GetEdmEntityTypeRef(ODataPath odataPath, out IEdmEntitySet entitySet)
        {
            entitySet = null;
            foreach (ODataPathSegment segment in odataPath)
            {
                var entitySegment = segment as EntitySetSegment;
                if (entitySegment != null)
                {
                    entitySet = entitySegment.EntitySet;
                    return (IEdmEntityTypeReference)((IEdmCollectionType)entitySegment.EdmType).ElementType;
                }
            }
            throw new InvalidOperationException("not supported type ODataPath");
        }
        private OeEntityItem ReadEntityFromStream(OeMessageContext context, Stream content)
        {
            var parser = new ODataUriParser(context.Model, context.BaseUri, RequestUrl);
            IEdmEntitySet entitySet;
            IEdmEntityTypeReference entityTypeRef = GetEdmEntityTypeRef(parser.ParsePath(), out entitySet);
            var entityType = (IEdmEntityType)entityTypeRef.Definition;

            ODataResource entry = null;
            IODataRequestMessage requestMessage = new OeInMemoryMessage(content, ContentType);
            var settings = new ODataMessageReaderSettings { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, context.Model))
            {
                ODataReader reader = messageReader.CreateODataResourceReader(entitySet, entityType);

                while (reader.Read())
                    if (reader.State == ODataReaderState.ResourceEnd)
                        entry = (ODataResource)reader.Item;
                if (entry == null)
                    throw new InvalidOperationException("operation not contain entry");
            }

            Db.OeEntitySetMetaAdapter entitySetMetaAdapter = context.EntitySetMetaAdapters.FindByEntitySetName(entitySet.Name);
            return new OeEntityItem(entitySet, entityType, entitySetMetaAdapter.EntityType, entry);
        }
        private OeEntityItem ReadEntityFromUrl(OeMessageContext context)
        {
            var parser = new ODataUriParser(context.Model, context.BaseUri, RequestUrl);

            ODataPath path = parser.ParsePath();
            var keySegment = (KeySegment)path.LastSegment;
            var entityType = (IEdmEntityType)keySegment.EdmType;
            var entitySet = (IEdmEntitySet)keySegment.NavigationSource;

            var properties = new List<ODataProperty>(1);
            foreach (var key in keySegment.Keys)
                properties.Add(new ODataProperty() { Name = key.Key, Value = key.Value });
            var entry = new ODataResource() { Properties = properties };

            Db.OeEntitySetMetaAdapter entitySetMetaAdapter = context.EntitySetMetaAdapters.FindByEntitySetName(entitySet.Name);
            return new OeEntityItem(entitySet, entityType, entitySetMetaAdapter.EntityType, entry);
        }

        public String ContentId
        {
            get
            {
                return _contentId;
            }
        }
        public String ContentType
        {
            get
            {
                return _contentType;
            }
        }
        public String Method
        {
            get
            {
                return _method;
            }
        }
        public Uri RequestUrl
        {
            get
            {
                return _requestUrl;
            }
        }
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
