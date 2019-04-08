using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCorePostgreSqlDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create(true)))
            {
                var model = (IMutableModel)dbContext.Model;
                model.Relational().DefaultSchema = "dbo";
                return new OeEfCoreEdmModelMetadataProvider(model);
            }
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : OeEfCoreDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
