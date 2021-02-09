using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public static class DynamicMiddlewareHelper
    {
        public static IEdmModel CreateEdmModel(ProviderSpecificSchema providerSchema, InformationSchemaSettings? informationSchemaSettings)
        {
            return CreateEdmModel(providerSchema, informationSchemaSettings ?? new InformationSchemaSettings(), new DynamicTypeDefinitionManagerFactory());
        }
        public static IEdmModel CreateEdmModel(ProviderSpecificSchema providerSchema, InformationSchemaSettings informationSchemaSettings, DynamicTypeDefinitionManagerFactory factory)
        {
            using (var metadataProvider = providerSchema.CreateMetadataProvider(informationSchemaSettings))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = factory.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                return dataAdapter.BuildEdmModel(metadataProvider);
            }
        }
        public static IEdmModel CreateEdmModelViaEmit(ProviderSpecificSchema providerSchema, InformationSchemaSettings informationSchemaSettings)
        {
            return CreateEdmModel(providerSchema, informationSchemaSettings, new EmitDynamicTypeDefinitionManagerFactory());
        }
    }
}

