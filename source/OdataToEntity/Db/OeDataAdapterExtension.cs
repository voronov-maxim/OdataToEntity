using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity
{
    public static class OeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter, params IEdmModel[] refModels)
        {
            return dataAdapter.BuildEdmModel(OeModelBoundAttribute.No, refModels);
        }
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter, OeModelBoundAttribute useModelBoundAttribute, params IEdmModel[] refModels)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEdmModelMetadataProvider(useModelBoundAttribute));
            return modelBuilder.BuildEdmModel(refModels);
        }
    }
}