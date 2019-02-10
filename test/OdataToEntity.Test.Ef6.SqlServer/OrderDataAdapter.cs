using OdataToEntity.Ef6;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Ef6.SqlServer;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEf6SqlServerDataAdapter<OrderEf6Context>, ITestDbDataAdapter
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
        private readonly bool _useRelationalNulls;

        public OrderDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
            _dbDataAdapter = new OrderDbDataAdapter(useRelationalNulls);
        }

        public override Object CreateDataContext()
        {
            return new OrderEf6Context(_useRelationalNulls);
        }
        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider(bool useRelationalNulls, String databaseName, bool useModelBoundAttribute)
        {
            using (var dbContext = new OrderEf6Context(false))
                return new OeEf6EdmModelMetadataProvider(dbContext, useModelBoundAttribute);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => _dbDataAdapter;
    }

    public sealed class Order2DataAdapter : OeEf6SqlServerDataAdapter<Order2Ef6Context>, ITestDbDataAdapter
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
        private readonly bool _useRelationalNulls;

        public Order2DataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
            _dbDataAdapter = new Order2DbDataAdapter(useRelationalNulls);
        }

        public override Object CreateDataContext()
        {
            return new Order2Ef6Context(_useRelationalNulls);
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => _dbDataAdapter;
    }
}