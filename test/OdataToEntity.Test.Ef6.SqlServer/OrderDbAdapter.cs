using OdataToEntity.Ef6;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Ef6.SqlServer;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private readonly bool _useRelationalNulls;

        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new Model.OrderContext(Model.OrderContextOptions.Create(_useRelationalNulls, null));
        }
    }

    public sealed class OrderOeDataAdapter : OeEf6DataAdapter<OrderEf6Context>
    {
        private readonly bool _useRelationalNulls;

        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderEf6Context(_useRelationalNulls);
        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}