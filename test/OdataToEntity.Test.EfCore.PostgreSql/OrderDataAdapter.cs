using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderDataAdapter : OeEfCorePostgreSqlDataAdapter<OrderContext>, ITestDbDataAdapter
    {
        public OrderDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, null), new Cache.OeQueryCache(allowCache))
        {
        }

        public static ModelBuilder.OeEdmModelMetadataProvider CreateMetadataProvider(bool useRelationalNulls, String databaseName, bool useModelBoundAttribute)
        {
            using (var dbContext = new OrderContext(OrderContextOptions.Create(useRelationalNulls, databaseName)))
            {
                var model = (IMutableModel)dbContext.Model;
                model.Relational().DefaultSchema = "dbo";
                return new OeEfCoreEdmModelMetadataProvider(model, useModelBoundAttribute);
            }
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }

    public sealed class Order2DataAdapter : OeEfCoreDataAdapter<Order2Context>, ITestDbDataAdapter
    {
        public Order2DataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create<Order2Context>(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }

        Db.OeDataAdapter ITestDbDataAdapter.DbDataAdapter => this;
    }
}
