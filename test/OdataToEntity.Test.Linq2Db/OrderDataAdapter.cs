extern alias lq2db;

using OdataToEntity.EfCore;
using OdataToEntity.Linq2Db;
using System;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;
using Order2Connection = lq2db::OdataToEntity.Test.Model.Order2Connection;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeLinq2DbSqlServerDataAdapter<OdataToEntityDB>, ITestDbDataAdapter
    {
        private sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<OrderContext>
        {
            private readonly bool _useRelationalNulls;

            public OrderDbDataAdapter(bool useRelationalNulls) : base(new Cache.OeQueryCache(false))
            {
                _useRelationalNulls = useRelationalNulls;
            }

            public override Object CreateDataContext()
            {
                return new OrderContext(OrderContextOptions.Create(_useRelationalNulls, null));
            }
        }

        private readonly Db.OeDataAdapter _dbDataAdapter;

        public OrderDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            LinqToDB.Common.Configuration.Linq.AllowMultipleQuery = true;
            _dbDataAdapter = new OrderDbDataAdapter(useRelationalNulls);
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider(bool useRelationalNulls, String databaseName, bool useModelBoundAttribute)
        {
            return new OeLinq2DbEdmModelMetadataProvider(useModelBoundAttribute);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => _dbDataAdapter;
    }

    public sealed class Order2DataAdapter : OeLinq2DbSqlServerDataAdapter<Order2Connection>, ITestDbDataAdapter
    {
        private sealed class Order2DbDataAdapter : OeEfCoreDataAdapter<Order2Context>
        {
            private readonly bool _useRelationalNulls;

            public Order2DbDataAdapter(bool useRelationalNulls) : base(new Cache.OeQueryCache(false))
            {
                _useRelationalNulls = useRelationalNulls;
            }

            public override Object CreateDataContext()
            {
                return new Order2Context(OrderContextOptions.Create<Order2Context>(_useRelationalNulls, null));
            }
        }

        private readonly Db.OeDataAdapter _dbDataAdapter;

        public Order2DataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            LinqToDB.Common.Configuration.Linq.AllowMultipleQuery = true;
            _dbDataAdapter = new Order2DbDataAdapter(useRelationalNulls);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => _dbDataAdapter;
    }
}
