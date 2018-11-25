using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderOeDataAdapter : OeEfCoreDataAdapter<OrderContext>
    {
        private readonly bool _useRelationalNulls;

        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls, null));
        }
        public ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            using (var dbContext = (DbContext)CreateDataContext())
                return new OeEfCoreEdmModelMetadataProvider(dbContext.Model);
        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}
