using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.EfCore
{
    public static class OeEfCoreDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEfCoreModel(this Db.OeDataAdapter dataAdapter, params IEdmModel[] refModels)
        {
            return dataAdapter.BuildEdmModelFromEfCoreModel(false, refModels);
        }
        public static EdmModel BuildEdmModelFromEfCoreModel(this Db.OeDataAdapter dataAdapter, bool useModelBoundAttribute, params IEdmModel[] refModels)
        {
            var context = (DbContext)dataAdapter.CreateDataContext();
            try
            {
                var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEfCoreEdmModelMetadataProvider(context.Model, useModelBoundAttribute));
                return modelBuilder.BuildEdmModel(refModels);
            }
            finally
            {
                dataAdapter.CloseDataContext(context);
            }
        }
    }
}
