using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider(bool useRelationalNulls, String databaseName, OeModelBoundAttribute useModelBoundAttribute)
        {
            return new ModelBuilder.OeEdmModelMetadataProvider(useModelBoundAttribute);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : EfCore.OeEfCoreDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
