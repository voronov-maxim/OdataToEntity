using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Query
{
    public readonly struct OeModelBoundAttributeReader
    {
        private readonly IEdmModel _edmModel;
        private readonly OeModelBoundQueryBuilder _modelBoundQueryBuilder;

        public OeModelBoundAttributeReader(IEdmModel edmModel)
        {
            _edmModel = edmModel;

            _modelBoundQueryBuilder = new OeModelBoundQueryBuilder();
        }

        private void BuildModelBoundAttribute(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            var pageAttribute = (PageAttribute)clrEntityType.GetCustomAttribute(typeof(PageAttribute));
            if (pageAttribute != null)
            {
                _edmModel.SetModelBoundAttribute(edmEntityType, pageAttribute);
                _modelBoundQueryBuilder.SetMaxTop(edmEntityType, pageAttribute.MaxTop);
            }

            var countAttribute = (CountAttribute)clrEntityType.GetCustomAttribute(typeof(CountAttribute));
            if (countAttribute != null && countAttribute.Disabled)
                _modelBoundQueryBuilder.SetCountable(edmEntityType, false);

            foreach (IEdmNavigationProperty navigationProperty in edmEntityType.NavigationProperties())
                if (navigationProperty.Type.IsCollection())
                {
                    PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCase(navigationProperty);

                    pageAttribute = (PageAttribute)clrProperty.GetCustomAttribute(typeof(PageAttribute));
                    if (pageAttribute != null)
                    {
                        _edmModel.SetModelBoundAttribute(navigationProperty, pageAttribute);
                        _modelBoundQueryBuilder.SetMaxTop(navigationProperty, pageAttribute.MaxTop);
                    }

                    countAttribute = (CountAttribute)clrProperty.GetCustomAttribute(typeof(CountAttribute));
                    if (countAttribute != null && countAttribute.Disabled)
                        _modelBoundQueryBuilder.SetCountable(navigationProperty, false);
                }
        }
        public OeModelBoundQueryProvider BuildProvider()
        {
            var filterAttributeReader = new OeFilterAttributeReader(_modelBoundQueryBuilder);
            var orderByAttributeReader = new OeOrderByAttributeReader(_modelBoundQueryBuilder);

            BuildByEdmModel(_edmModel, filterAttributeReader, orderByAttributeReader);
            foreach (IEdmModel refModel in _edmModel.ReferencedModels)
                BuildByEdmModel(refModel, filterAttributeReader, orderByAttributeReader);

            return _modelBoundQueryBuilder.Build();
        }
        private void BuildByEdmModel(IEdmModel edmModel, OeFilterAttributeReader filterAttributeReader, OeOrderByAttributeReader orderByAttributeReader)
        {
            foreach (IEdmSchemaElement schemaElement in edmModel.SchemaElements)
                if (schemaElement is IEdmEntityType edmEntityType)
                {
                    Type clrEntityType = edmModel.GetAnnotationValue<Type>(edmEntityType);
                    if (clrEntityType == null)
                        continue;

                    int level = 3;
                    BuildSelectExpandAttribute(edmEntityType, clrEntityType, new HashSet<PropertyInfo>(), level);
                    BuildModelBoundAttribute(edmEntityType, clrEntityType);
                    filterAttributeReader.Build(edmEntityType, clrEntityType);
                    orderByAttributeReader.Build(edmEntityType, clrEntityType);
                }
        }
        private SelectItem[] BuildSelectExpandAttribute(IEdmEntityType edmEntityType, Type clrEntityType, HashSet<PropertyInfo> visited, int level)
        {
            if (level < 0)
                return Array.Empty<SelectItem>();

            var selectExpandItems = new List<SelectItem>();
            foreach (IEdmProperty edmProperty in edmEntityType.Properties())
            {
                PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCaseOrNull(edmProperty);
                if (clrProperty == null) //shadow property
                    continue;

                if (edmProperty is IEdmNavigationProperty navigationProperty && level > 0)
                {
                    var expandAttribute = (ExpandAttribute)clrProperty.GetCustomAttribute(typeof(ExpandAttribute));
                    if (expandAttribute == null)
                        continue;

                    if (expandAttribute.ExpandType == SelectExpandType.Disabled)
                        _modelBoundQueryBuilder.SetExpandable(edmProperty, false);

                    if (expandAttribute.ExpandType != SelectExpandType.Automatic)
                        continue;

                    if (expandAttribute.MaxDepth > 0 && expandAttribute.MaxDepth < level)
                        level = expandAttribute.MaxDepth;

                    IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
                    var segment = new NavigationPropertySegment(navigationProperty, entitySet);

                    IEdmEntityType childEdmEntityType = entitySet.EntityType();
                    Type childClrEntityType = _edmModel.GetClrType(childEdmEntityType);

                    SelectItem[] childSelectExpandItems = Array.Empty<SelectItem>();
                    if (visited.Add(clrProperty))
                    {
                        childSelectExpandItems = BuildSelectExpandAttribute(childEdmEntityType, childClrEntityType, visited, level - 1);
                        visited.Remove(clrProperty);
                    }
                    else
                    {
                        if (level >= 0)
                            childSelectExpandItems = BuildSelectExpandAttribute(childEdmEntityType, childClrEntityType, visited, 0);
                    }

                    var selectExpandClause = new SelectExpandClause(childSelectExpandItems, false);
                    selectExpandItems.Add(new ExpandedNavigationSelectItem(new ODataExpandPath(segment), entitySet, selectExpandClause));
                }
                else if (edmProperty is IEdmStructuralProperty structuralProperty)
                {
                    var selectAttribute = (SelectAttribute)clrProperty.GetCustomAttribute(typeof(SelectAttribute));
                    if (selectAttribute == null)
                        continue;

                    if (selectAttribute.SelectType == SelectExpandType.Automatic)
                        selectExpandItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty))));
                    else if (selectAttribute.SelectType == SelectExpandType.Disabled)
                    {
                        selectExpandItems.Add(new Parsers.Translators.OeDisableSelectItem(structuralProperty));
                        _modelBoundQueryBuilder.SetExpandable(edmProperty, false);
                    }
                }
            }

            if (selectExpandItems.Count == 0)
                return Array.Empty<ExpandedNavigationSelectItem>();

            SelectItem[] selectExpandItemArray = selectExpandItems.ToArray();
            _modelBoundQueryBuilder.SetSelectExpandItems(edmEntityType, selectExpandItemArray);
            return selectExpandItemArray;
        }
    }
}
