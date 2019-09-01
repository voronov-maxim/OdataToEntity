using System.Linq.Expressions;
using OdataToEntity.EfCore;
using OdataToEntity.EfCore.Postgresql;

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
                return new OeEfCoreEdmModelMetadataProvider(dbContext.Model);
        }
        protected override Expression TranslateExpression(Expression expression)
        {
            return new EfCore.Postgresql.OeDateTimeOffsetMembersVisitor().Visit(expression);
        }
    }

    public sealed class Order2DataAdapter : OeEfCorePostgreSqlDataAdapter<Order2Context>
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls), new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
