using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(Model.OrderContextOptions.Create(useRelationalNulls, ""), new Db.OeQueryCache(allowCache))
        {
        }
    }

    public sealed partial class OrderOeDataAdapter : OeEfCorePostgreSqlDataAdapter<Model.OrderContext>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(Model.OrderContextOptions.Create(useRelationalNulls, null), new Db.OeQueryCache(allowCache))
        {
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
