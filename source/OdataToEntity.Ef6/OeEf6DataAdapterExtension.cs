using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System.Data.Entity;

namespace OdataToEntity.Ef6
{
    public static class OeEf6DataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEf6Model(this Db.OeDataAdapter dataAdapter)
        {
            using (var context = (DbContext)dataAdapter.CreateDataContext())
            {
                var metadataProvider = new OeEf6EdmModelMetadataProvider(context);
                var modelBuilder = new OeEdmModelBuilder(metadataProvider);
                modelBuilder.AddEntitySetRange(dataAdapter.EntitySetAdapters.GetEntitySetNamesEntityTypes());
                OeDataAdapterExtension.BuildOperations(dataAdapter, modelBuilder);
                return modelBuilder.BuildEdmModel();
            }
        }
    }
}
