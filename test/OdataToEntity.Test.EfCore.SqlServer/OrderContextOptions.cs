using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static EdmModel BuildEdmModel(Db.OeDataAdapter dataAdapter, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            bool allowCache = TestHelper.GetQueryCache(dataAdapter).AllowCache;
            var order2DataAdapter = new Order2DataAdapter(allowCache, true);
            var refModel = new ModelBuilder.OeEdmModelBuilder(dataAdapter, metadataProvider).BuildEdmModel();
            return order2DataAdapter.BuildEdmModel(refModel);
        }
        public static DbContextOptions Create(bool useRelationalNulls)
        {
            return Create<OrderContext>(useRelationalNulls);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls) where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        public static DbContextOptions CreateClientEvaluationWarning(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls))
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.QueryClientEvaluationWarning));
            return optionsBuilder.Options;
        }
    }
}