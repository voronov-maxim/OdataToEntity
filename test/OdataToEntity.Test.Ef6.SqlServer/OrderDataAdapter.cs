using OdataToEntity.Ef6;
using OdataToEntity.Test.Ef6.SqlServer;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEf6SqlServerDataAdapter<OrderEf6Context>
    {
        private readonly bool _useRelationalNulls;

        public OrderDataAdapter(bool allowCache, bool useRelationalNulls) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderEf6Context(_useRelationalNulls);
        }
        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderEf6Context(false))
                return new OeEf6EdmModelMetadataProvider(dbContext);
        }
    }

    public sealed class Order2DataAdapter : OeEf6SqlServerDataAdapter<Order2Ef6Context>
    {
        public readonly bool _useRelationalNulls;

        public Order2DataAdapter(bool allowCache, bool useRelationalNulls) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new Order2Ef6Context(_useRelationalNulls);
        }
    }
}