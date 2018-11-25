using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;

namespace OdataToEntity
{
    public static class OeDataAdapterExtension
    {
        public static EdmModel BuildEdmModel(this Db.OeDataAdapter dataAdapter)
        {
            var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEdmModelMetadataProvider());
            return modelBuilder.BuildEdmModel();
        }
    }
}