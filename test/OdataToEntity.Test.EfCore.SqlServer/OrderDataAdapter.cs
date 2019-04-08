using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCoreSqlServerDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        private readonly bool _useRelationalNulls;

        public OrderDataAdapter() : this(true, true)
        {
        }
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls));
        }
        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true)))
                return new OeEfCoreEdmModelMetadataProvider(dbContext.Model);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : OeEfCoreSqlServerDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
