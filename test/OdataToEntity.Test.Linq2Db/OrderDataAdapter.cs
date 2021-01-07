extern alias lq2db;

using OdataToEntity.EfCore;
using OdataToEntity.Linq2Db;

using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;
using Order2Connection = lq2db::OdataToEntity.Test.Model.Order2Connection;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeLinq2DbSqlServerDataAdapter<OdataToEntityDB>
    {
        private sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<OrderContext>
        {
            public OrderDbDataAdapter(bool useRelationalNulls) : base(new Cache.OeQueryCache(false))
            {
            }
        }

        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) : base(new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            return new OeLinq2DbEdmModelMetadataProvider();
        }
    }

    public sealed class Order2DataAdapter : OeLinq2DbSqlServerDataAdapter<Order2Connection>
    {
        private sealed class Order2DbDataAdapter : OeEfCoreDataAdapter<Order2Context>
        {
            public Order2DbDataAdapter(bool useRelationalNulls) : base(new Cache.OeQueryCache(false))
            {
            }
        }

        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) : base(new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
