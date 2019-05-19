using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static EdmModel BuildEdmModel(Db.OeDataAdapter dataAdapter, ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
        {
            return new ModelBuilder.OeEdmModelBuilder(dataAdapter, metadataProvider).BuildEdmModel();
        }
        public static DbContextOptions Create(bool useRelationalNulls)
        {
            return DynamicDbContext.CreateOptions();
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls) where T : DbContext
        {
            throw new NotSupportedException();
        }
    }
}