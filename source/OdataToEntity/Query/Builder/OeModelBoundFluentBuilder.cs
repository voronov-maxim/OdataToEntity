using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundFluentBuilder
    {
        public OeModelBoundFluentBuilder(IEdmModel edmModel)
        {
            EdmModel = edmModel;
            ModelBoundSettingsBuilder = new OeModelBoundSettingsBuilder();
        }

        public EntitySetConfiguration<TEntityType> EntitySet<TEntityType>(String entitySetName) where TEntityType : class
        {
            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, entitySetName);
            return new EntitySetConfiguration<TEntityType>(this, entitySet);
        }
        public OeModelBoundProvider BuildProvider()
        {
            return ModelBoundSettingsBuilder.Build();
        }

        public IEdmModel EdmModel { get; }
        public OeModelBoundSettingsBuilder ModelBoundSettingsBuilder { get; }
    }
}
