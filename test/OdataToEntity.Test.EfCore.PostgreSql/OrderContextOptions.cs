using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public static class OrderContextOptions
    {
        internal static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }

    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<OrderContext>
    {
        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, ""), new Cache.OeQueryCache(allowCache))
        {
            base.IsDatabaseNullHighestValue = true;
        }
    }
}

namespace OdataToEntity.Test
{
    public static class OrderOeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this Model.OrderOeDataAdapter dataAdapter)
        {
            return dataAdapter.BuildEdmModelFromEfCorePgSqlModel("dbo");
        }
    }
}
