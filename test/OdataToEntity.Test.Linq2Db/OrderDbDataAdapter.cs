extern alias lq2db;
using OdataToEntity.EfCore;
using OdataToEntity.Linq2Db;
using System;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

namespace OdataToEntity.Test
{
    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<Model.OrderContext>
    {
        private readonly bool _useRelationalNulls;

        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Db.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new Model.OrderContext(Model.OrderContextOptions.Create(_useRelationalNulls, null));
        }
    }

    public sealed class OrderOeDataAdapter : OeLinq2DbDataAdapter<OdataToEntityDB>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Db.OeQueryCache(allowCache))
        {
        }

        public override Object CreateDataContext()
        {
            LinqToDB.Common.Configuration.Linq.AllowMultipleQuery = true;
            return new OdataToEntityDB("OdataToEntity");
        }

        public new Db.OeQueryCache QueryCache => base.QueryCache;
    }
}
