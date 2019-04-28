using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        public OrderDataAdapter(String databaseName) :
            base(OrderContextOptions.Create(databaseName), new Cache.OeQueryCache(false))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            return new ModelBuilder.OeEdmModelMetadataProvider();
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : EfCore.OeEfCoreDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, String databaseName) :
            base(OrderContextOptions.Create<Order2Context>(databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
