using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public static class DynamicMiddlewareHelper
    {
        public static IEdmModel CreateEdmModel(ProviderSpecificSchema providerSchema, InformationSchemaSettings informationSchemaSettings)
        {
            using (var metadataProvider = providerSchema.CreateMetadataProvider(informationSchemaSettings))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                return dataAdapter.BuildEdmModel(metadataProvider);
            }
        }
    }
}

