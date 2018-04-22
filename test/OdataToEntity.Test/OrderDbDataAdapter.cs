using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(Model.OrderContextOptions.Create(useRelationalNulls, databaseName), new Db.OeQueryCache(allowCache))
        {
        }
    }

    public sealed class OrderOeDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(Model.OrderContextOptions.Create(useRelationalNulls, databaseName), new Db.OeQueryCache(allowCache))
        {
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
