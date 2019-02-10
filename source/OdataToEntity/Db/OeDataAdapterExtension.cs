using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity
{
    public static class OeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter, params IEdmModel[] refModels)
        {
            return dataAdapter.BuildEdmModel(false, refModels);
        }
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter, bool useModelBoundAttribute, params IEdmModel[] refModels)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEdmModelMetadataProvider(useModelBoundAttribute));
            return modelBuilder.BuildEdmModel(refModels);
        }
    }
}