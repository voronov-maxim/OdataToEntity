using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCoreSqlServerDataAdapter<OrderContext>
    {
        public OrderDataAdapter() : this(true, true)
        {
        }
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true)))
                return new OeEfCoreEdmModelMetadataProvider(dbContext.Model);
        }
    }

    public sealed class Order2DataAdapter : OeEfCoreSqlServerDataAdapter<Order2Context>
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
