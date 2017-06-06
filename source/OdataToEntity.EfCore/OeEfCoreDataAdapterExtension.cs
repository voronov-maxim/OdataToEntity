using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.EfCore
{
    public static class OeEfCoreDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEfCoreModel(this Db.OeDataAdapter dataAdapter)
        {
            using (var context = (DbContext)dataAdapter.CreateDataContext())
            {
                var metadataProvider = new OeEfCoreEdmModelMetadataProvider(context.Model);
                var modelBuilder = new OeEdmModelBuilder(metadataProvider, dataAdapter.EntitySetMetaAdapters.ToDictionary());
                Db.OeDataAdapterExtension.BuildOperations(dataAdapter, modelBuilder);
                return modelBuilder.BuildEdmModel();
            }
        }
    }
}
