using Microsoft.OData.Edm;
using System;

namespace OdataToEntity
{
    public sealed class OeMessageContext
    {
        private readonly Uri _baseUri;
        private readonly Db.OeEntitySetMetaAdapterCollection _entitySetMetaAdapters;
        private readonly IEdmModel _model;

        public OeMessageContext(Uri baseUri, IEdmModel model, Db.OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
        {
            _baseUri = baseUri;
            _model = model;
            _entitySetMetaAdapters = entitySetMetaAdapters;
        }

        public Uri BaseUri => _baseUri;
        public Db.OeEntitySetMetaAdapterCollection EntitySetMetaAdapters => _entitySetMetaAdapters;
        public IEdmModel Model => _model;
    }
}
