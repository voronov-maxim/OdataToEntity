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
                var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEfCoreEdmModelMetadataProvider(context.Model));
                return modelBuilder.BuildEdmModel();
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
    }
}
