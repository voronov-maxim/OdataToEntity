using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity.Linq2Db
{
    public static class OeLinq2DbDataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromLinq2DbModel(this Db.OeDataAdapter dataAdapter, params IEdmModel[] refModels)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeLinq2DbEdmModelMetadataProvider());
            return modelBuilder.BuildEdmModel(refModels);
        }
    }
}
