using OdataToEntity.Ef6;
using OdataToEntity.Test.Ef6.SqlServer;
using System;

namespace OdataToEntity.Test.Model
{
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
        public ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = new OrderEf6Context(false))
                return new OeEf6EdmModelMetadataProvider(dbContext);
        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}