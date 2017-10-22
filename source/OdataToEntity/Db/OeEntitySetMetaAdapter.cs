using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OdataToEntity.Db
{
    public abstract class OeEntitySetMetaAdapter
    {
        public abstract void AddEntity(Object dataContext, Object entity);
        public abstract void AttachEntity(Object dataContext, Object entity);
        public abstract IQueryable GetEntitySet(Object dataContext);
        public abstract void RemoveEntity(Object dataContext, Object entity);

        public abstract Type EntityType
        {
            get;
        }
        public abstract String EntitySetName
        {
            get;
        }
    }

    public sealed class OeEntitySetMetaAdapterCollection : ReadOnlyCollection<OeEntitySetMetaAdapter>
    {
        private readonly ModelBuilder.OeEdmModelMetadataProvider _metadataProvider;

        public OeEntitySetMetaAdapterCollection(OeEntitySetMetaAdapter[] entitySetMetaAdapter,
            ModelBuilder.OeEdmModelMetadataProvider metadataProvider) : base(entitySetMetaAdapter)
        {
            _metadataProvider = metadataProvider;
        }

        public OeEntitySetMetaAdapter FindByClrType(Type entityType)
        {
            var entitySetMetaAdapters = (OeEntitySetMetaAdapter[])base.Items;
            foreach (OeEntitySetMetaAdapter entitySetMetaAdapter in entitySetMetaAdapters)
                if (entitySetMetaAdapter.EntityType == entityType)
                    return entitySetMetaAdapter;
            return null;
        }
        public OeEntitySetMetaAdapter FindByEntitySetName(String entitySetName)
        {
            var entitySetMetaAdapters = (OeEntitySetMetaAdapter[])base.Items;
            foreach (OeEntitySetMetaAdapter entitySetMetaAdapter in entitySetMetaAdapters)
                if (entitySetMetaAdapter.EntitySetName == entitySetName)
                    return entitySetMetaAdapter;
            return null;
        }
        public OeEntitySetMetaAdapter FindByTypeName(String typeName)
        {
            var entitySetMetaAdapters = (OeEntitySetMetaAdapter[])base.Items;
            foreach (OeEntitySetMetaAdapter entitySetMetaAdapter in entitySetMetaAdapters)
                if (entitySetMetaAdapter.EntityType.FullName == typeName)
                    return entitySetMetaAdapter;
            return null;
        }
        public IEnumerable<KeyValuePair<String, Type>> GetEntitySetNamesEntityTypes()
        {
            var entitySetMetaAdapters = (OeEntitySetMetaAdapter[])base.Items;
            foreach (OeEntitySetMetaAdapter entitySetMetaAdapter in entitySetMetaAdapters)
                yield return new KeyValuePair<string, Type>(entitySetMetaAdapter.EntitySetName, entitySetMetaAdapter.EntityType);
        }

        public ModelBuilder.OeEdmModelMetadataProvider EdmModelMetadataProvider => _metadataProvider;
    }
}
