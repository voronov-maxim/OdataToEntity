using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.Linq2Db
{
    public static class OeLinq2DbDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromLinq2DbModel(this Db.OeDataAdapter dataAdapter, params IEdmModel[] refModels)
        {
            return dataAdapter.BuildEdmModelFromLinq2DbModel(OeModelBoundAttribute.No, refModels);
        }
        public static EdmModel BuildEdmModelFromLinq2DbModel(this Db.OeDataAdapter dataAdapter, OeModelBoundAttribute useModelBoundAttribute, params IEdmModel[] refModels)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeLinq2DbEdmModelMetadataProvider(useModelBoundAttribute));
            return modelBuilder.BuildEdmModel(refModels);
        }
    }
}
