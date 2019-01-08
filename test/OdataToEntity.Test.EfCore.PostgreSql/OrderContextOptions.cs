using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.Test.Model
{
    public static class OrderContextOptions
    {
        public static EdmModel BuildEdmModel(Db.OeDataAdapter dataAdapter, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            bool allowCache = TestHelper.GetQueryCache(dataAdapter).AllowCache;
            var order2DataAdapter = new Order2DataAdapter(allowCache, true, "test2");
            var refModel = new ModelBuilder.OeEdmModelBuilder(dataAdapter, metadataProvider).BuildEdmModel();
            return order2DataAdapter.BuildEdmModel(refModel);
        }
        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            return Create<OrderContext>(useRelationalNulls, databaseName);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls, String databaseName) where T : DbContext
        {
            Npgsql.NpgsqlConnection.GlobalTypeMapper.MapComposite<OdataToEntity.EfCore.StringList>("dbo.string_list");

            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}