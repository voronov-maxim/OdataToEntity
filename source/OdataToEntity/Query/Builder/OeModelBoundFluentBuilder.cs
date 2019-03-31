using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
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
            if (entitySet == null)
                throw new InvalidOperationException("EntitySet " + entitySetName + " not found in EdmModel");

            return new EntitySetConfiguration<TEntityType>(this, entitySet);
        }
        private void BuildByEdmModel(IEdmModel edmModel)
        {
            var selectExpandBuilder = new OeSelectExpandBuilder(edmModel, ModelBoundSettingsBuilder);
            foreach (IEdmSchemaElement element in edmModel.SchemaElements)
                if (element is IEdmEntityType entityType)
                    if (edmModel.GetAnnotationValue<Type>(entityType) != null)
                    {
                        SelectItem[] selectItems = selectExpandBuilder.Build(entityType, 3);
                        if (selectItems.Length > 0)
                            ModelBoundSettingsBuilder.SetSelectExpandItems(entityType, selectItems);
                    }
        }
        public OeModelBoundProvider BuildProvider()
        {
            BuildByEdmModel((EdmModel)EdmModel);
            foreach (IEdmModel refModel in EdmModel.ReferencedModels)
                if (refModel is EdmModel)
                    BuildByEdmModel(refModel);

            return ModelBoundSettingsBuilder.Build();
        }

        public IEdmModel EdmModel { get; }
        public OeModelBoundSettingsBuilder ModelBoundSettingsBuilder { get; }
    }
}
