using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed partial class OrderOeDataAdapter : OeEfCorePostgreSqlDataAdapter<OrderContext>
    {
        public OrderOeDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, null), new Cache.OeQueryCache(allowCache))
        {
        }

        public ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider()
        {
            var context = (DbContext)base.CreateDataContext();
            try
            {
                var model = (Microsoft.EntityFrameworkCore.Metadata.Internal.Model)context.Model;
                model.Relational().DefaultSchema = "dbo";
                return new OeEfCoreEdmModelMetadataProvider(model);
            }
            finally
            {
                base.CloseDataContext(context);
            }

        }

        public new Cache.OeQueryCache QueryCache => base.QueryCache;
    }
}
