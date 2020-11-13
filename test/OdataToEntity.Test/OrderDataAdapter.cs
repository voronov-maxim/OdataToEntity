using Microsoft.OData.Edm;
using System.Linq.Expressions;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>
    {
        public OrderDataAdapter() :
            base(OrderContextOptions.Create(OrderContext.GenerateDatabaseName()), new Cache.OeQueryCache(false))
        {
        }

        protected override Expression TranslateExpression(IEdmModel edmModel, Expression expression)
        {
            return new SQLiteVisitor().Visit(expression);
        }
    }

    public sealed class Order2DataAdapter : EfCore.OeEfCoreDataAdapter<Order2Context>
    {
        public Order2DataAdapter() :
            base(OrderContextOptions.Create<Order2Context>(OrderContext.GenerateDatabaseName()), new Cache.OeQueryCache(false))
        {
        }
    }
}