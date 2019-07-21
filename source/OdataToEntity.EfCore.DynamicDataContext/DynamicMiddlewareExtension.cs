using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public static class DynamicMiddlewareExtension
    {
        private static IEdmModel CreateEdmModel(ProviderSpecificSchema providerSchema, InformationSchemaMapping informationSchemaMapping)
        {
            using (var metadataProvider = providerSchema.CreateMetadataProvider(informationSchemaMapping))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                return dataAdapter.BuildEdmModel(metadataProvider);
            }
        }
        public static IApplicationBuilder DynamicMiddleware(this IApplicationBuilder app, PathString apiPath, ProviderSpecificSchema providerSchema, InformationSchemaMapping informationSchemaMapping)
        {
            return app.UseOdataToEntityMiddleware<OeMiddleware>(apiPath, CreateEdmModel(providerSchema, informationSchemaMapping));
        }
    }
}
