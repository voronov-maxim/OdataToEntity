using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCoreDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        private readonly bool _useRelationalNulls;

        public OrderDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls, null));
        }
        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider(bool useRelationalNulls, String databaseName)
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create(useRelationalNulls, databaseName)))
                return new OeEfCoreEdmModelMetadataProvider(dbContext.Model);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : OeEfCoreDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
