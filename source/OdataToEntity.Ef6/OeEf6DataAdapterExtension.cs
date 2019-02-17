using Microsoft.OData.Edm;
using OdataToEntity.ModelBuilder;
using System.Data.Entity;

namespace OdataToEntity.Ef6
{
    public static class OeEf6DataAdapterExtension
    {
        public static EdmModel BuildEdmModelFromEf6Model(this Db.OeDataAdapter dataAdapter)
        {
            return dataAdapter.BuildEdmModelFromEf6Model(OeModelBoundAttribute.No);
        }
        public static EdmModel BuildEdmModelFromEf6Model(this Db.OeDataAdapter dataAdapter, OeModelBoundAttribute useModelBoundAttribute)
        {
            using (var context = (DbContext)dataAdapter.CreateDataContext())
            {
                var modelBuilder = new OeEdmModelBuilder(dataAdapter, new OeEf6EdmModelMetadataProvider(context, useModelBoundAttribute));
                return modelBuilder.BuildEdmModel();
            }
        }
    }
}
