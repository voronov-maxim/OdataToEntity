using Microsoft.OData.Edm;
using OdataToEntity.Query;
using OdataToEntity.Query.Builder;

namespace OdataToEntity.Test
{
    public readonly struct PageNextLinkModelBoundBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly bool _sqlite;

        public PageNextLinkModelBoundBuilder(IEdmModel edmModel, bool sqlite)
        {
            _edmModel = edmModel;
            _sqlite = sqlite;
        }

        private void Build(IEdmModel edmModel, OeModelBoundSettingsBuilder modelBoundSettingsBuilder, int pageSize, bool navigationNextLink)
        {
            if (edmModel.EntityContainer != null)
                foreach (IEdmEntitySet entitySet in edmModel.EntityContainer.EntitySets())
                {
                    IEdmEntityType entityType = entitySet.EntityType();
                    modelBoundSettingsBuilder.SetPageSize(pageSize, entityType);

                    foreach (IEdmNavigationProperty navigationProperty in entityType.NavigationProperties())
                    {
                        if (navigationNextLink)
                            modelBoundSettingsBuilder.SetNavigationNextLink(navigationNextLink, navigationProperty);

                        if (navigationProperty.Type.IsCollection())
                        {

                            if (_sqlite)
                                modelBoundSettingsBuilder.SetPageSize(-1, navigationProperty);
                        }
                    }
                }

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                Build(refModel, modelBoundSettingsBuilder, pageSize, navigationNextLink);
        }
        public OeModelBoundProvider BuildProvider(int pageSize, bool navigationNextLink)
        {
            if (pageSize > 0 || navigationNextLink)
            {
                var modelBoundSettingsBuilder = new OeModelBoundSettingsBuilder();
                Build(_edmModel, modelBoundSettingsBuilder, pageSize, navigationNextLink);
                return modelBoundSettingsBuilder.Build();
            }

            return null;
        }
    }
}
