using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderOeDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        public ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            return new ModelBuilder.OeEdmModelMetadataProvider();
        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}
