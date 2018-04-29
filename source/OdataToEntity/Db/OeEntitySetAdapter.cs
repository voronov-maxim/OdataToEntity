using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OdataToEntity.Db
{
    public abstract class OeEntitySetAdapter
    {
        public abstract void AddEntity(Object dataContext, ODataResourceBase entry);
        public abstract void AttachEntity(Object dataContext, ODataResourceBase entry);
        public abstract IQueryable GetEntitySet(Object dataContext);
        public abstract void RemoveEntity(Object dataContext, ODataResourceBase entry);

        public abstract Type EntityType { get; }
        public abstract String EntitySetName { get; }
    }

    public sealed class OeEntitySetAdapterCollection : ReadOnlyCollection<OeEntitySetAdapter>
    {
        public OeEntitySetAdapterCollection(OeEntitySetAdapter[] entitySetAdapters,
            ModelBuilder.OeEdmModelMetadataProvider metadataProvider) : base(entitySetAdapters)
        {
            EdmModelMetadataProvider = metadataProvider;
        }

        public OeEntitySetAdapter FindByClrType(Type entityType)
        {
            var entitySetAdapters = (OeEntitySetAdapter[])base.Items;
            foreach (OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (entitySetAdapter.EntityType == entityType)
                    return entitySetAdapter;
            return null;
        }
        public OeEntitySetAdapter FindByEntitySetName(String entitySetName)
        {
            var entitySetAdapters = (OeEntitySetAdapter[])base.Items;
            foreach (OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (entitySetAdapter.EntitySetName == entitySetName)
                    return entitySetAdapter;
            return null;
        }
        public OeEntitySetAdapter FindByTypeName(String typeName)
        {
            var entitySetAdapters = (OeEntitySetAdapter[])base.Items;
            foreach (OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                if (entitySetAdapter.EntityType.FullName == typeName)
                    return entitySetAdapter;
            return null;
        }
        public IEnumerable<KeyValuePair<String, Type>> GetEntitySetNamesEntityTypes()
        {
            var entitySetAdapters = (OeEntitySetAdapter[])base.Items;
            foreach (OeEntitySetAdapter entitySetAdapter in entitySetAdapters)
                yield return new KeyValuePair<string, Type>(entitySetAdapter.EntitySetName, entitySetAdapter.EntityType);
        }

        public ModelBuilder.OeEdmModelMetadataProvider EdmModelMetadataProvider { get; }
    }
}
