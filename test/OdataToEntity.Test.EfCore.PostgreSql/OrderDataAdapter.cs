using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCorePostgreSqlDataAdapter<OrderContext>
    {
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<OrderContext>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create<OrderContext>(true)))
            {
                var model = (IMutableModel)dbContext.Model;
                model.Relational().DefaultSchema = "dbo";
                return new OeEfCoreEdmModelMetadataProvider(model);
            }
        }
    }

    public sealed class Order2DataAdapter : OeEfCoreDataAdapter<Order2Context>
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
