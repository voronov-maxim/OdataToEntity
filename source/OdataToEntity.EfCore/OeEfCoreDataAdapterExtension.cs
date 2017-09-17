using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.EfCore
{
    public static class OeEfCoreDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEfCoreModel(this Db.OeDataAdapter dataAdapter)
        {
            var context = (DbContext)dataAdapter.CreateDataContext();
            try
            {
                var metadataProvider = new OeEfCoreEdmModelMetadataProvider(context.Model);
                var modelBuilder = new OeEdmModelBuilder(metadataProvider, dataAdapter.EntitySetMetaAdapters.ToDictionary());
                Db.OeDataAdapterExtension.BuildOperations(dataAdapter, modelBuilder);
                return modelBuilder.BuildEdmModel();
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
    }
}
