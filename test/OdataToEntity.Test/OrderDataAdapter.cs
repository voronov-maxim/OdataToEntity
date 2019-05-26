namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>
    {
        public OrderDataAdapter() :
            base(OrderContextOptions.Create(OrderContext.GenerateDatabaseName()), new Cache.OeQueryCache(false))
        {
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
