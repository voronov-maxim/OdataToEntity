using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Reflection;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OeModelBoundAttributeBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly OeModelBoundSettingsBuilder _modelBoundSettingsBuilder;

        public OeModelBoundAttributeBuilder(IEdmModel edmModel)
        {
            _edmModel = edmModel;

            _modelBoundSettingsBuilder = new OeModelBoundSettingsBuilder();
        }

        private void BuildModelBoundCountPage(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            var pageAttribute = (PageAttribute?)clrEntityType.GetCustomAttribute(typeof(PageAttribute));
            if (pageAttribute != null)
            {
                _modelBoundSettingsBuilder.SetPageSize(pageAttribute.PageSize, edmEntityType);
                _modelBoundSettingsBuilder.SetMaxTop(pageAttribute.MaxTop, edmEntityType);
            }

            var countAttribute = (CountAttribute?)clrEntityType.GetCustomAttribute(typeof(CountAttribute));
            if (countAttribute != null && countAttribute.Disabled)
                _modelBoundSettingsBuilder.SetCount(false, edmEntityType);

            foreach (IEdmNavigationProperty navigationProperty in edmEntityType.NavigationProperties())
            {
                PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCase(navigationProperty);

                bool isCollection = navigationProperty.Type.IsCollection();
                if (isCollection)
                {
                    countAttribute = (CountAttribute?)clrProperty.GetCustomAttribute(typeof(CountAttribute));
                    if (countAttribute != null && countAttribute.Disabled)
                        _modelBoundSettingsBuilder.SetCount(false, navigationProperty);
                }

                pageAttribute = (PageAttribute?)clrProperty.GetCustomAttribute(typeof(PageAttribute));
                if (pageAttribute != null)
                {
                    if (isCollection)
                    {
                        _modelBoundSettingsBuilder.SetPageSize(pageAttribute.PageSize, navigationProperty);
                        _modelBoundSettingsBuilder.SetMaxTop(pageAttribute.MaxTop, navigationProperty);
                    }
                    _modelBoundSettingsBuilder.SetNavigationNextLink(pageAttribute.NavigationNextLink, navigationProperty);
                }
            }
        }
        public OeModelBoundProvider BuildProvider()
        {
            BuildByEdmModel(_edmModel);
            foreach (IEdmModel refModel in _edmModel.ReferencedModels)
                if (refModel is EdmModel)
                    BuildByEdmModel(refModel);

            return _modelBoundSettingsBuilder.Build();
        }
        private void BuildByEdmModel(IEdmModel edmModel)
        {
            var expandAttributeReader = new OeAttributeReader<ExpandAttribute>(_modelBoundSettingsBuilder);
            var filterAttributeReader = new OeAttributeReader<FilterAttribute>(_modelBoundSettingsBuilder);
            var orderByAttributeReader = new OeAttributeReader<OrderByAttribute>(_modelBoundSettingsBuilder);
            var selectAttributeReader = new OeAttributeReader<SelectAttribute>(_modelBoundSettingsBuilder);
            foreach (IEdmSchemaElement schemaElement in edmModel.SchemaElements)
                if (schemaElement is IEdmEntityType edmEntityType)
                {
                    Type clrEntityType = edmModel.GetAnnotationValue<Type>(edmEntityType);
                    if (clrEntityType != null)
                    {
                        BuildModelBoundCountPage(edmEntityType, clrEntityType);
                        expandAttributeReader.Build(edmEntityType, clrEntityType);
                        filterAttributeReader.Build(edmEntityType, clrEntityType);
                        orderByAttributeReader.Build(edmEntityType, clrEntityType);
                        selectAttributeReader.Build(edmEntityType, clrEntityType);
                    }
                }
        }
    }
}
