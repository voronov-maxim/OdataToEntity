using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal readonly struct ModelBoundAttributeBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly Dictionary<Type, EntityTypeInfo> _entityTypeInfos;

        public ModelBoundAttributeBuilder(IEdmModel edmModel, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            _edmModel = edmModel;
            _entityTypeInfos = entityTypeInfos;
        }

        public void BuildExpandAttribute()
        {
            foreach (EntityTypeInfo typeInfo in _entityTypeInfos.Values)
            {
                int level = 2;
                BuildExpandAttribute(typeInfo, ref level);
            }
        }
        private SelectItem[] BuildExpandAttribute(EntityTypeInfo entityTypeInfo, ref int level)
        {
            if (--level < 0)
                return Array.Empty<SelectItem>();

            SelectItem[] selectItemArray = _edmModel.GetSelectItems(entityTypeInfo.EdmType);
            if (selectItemArray != null)
                return selectItemArray;

            var selectItems = new List<SelectItem>();
            foreach (IEdmProperty edmProperty in entityTypeInfo.EdmType.Properties())
            {
                PropertyInfo clrProperty = entityTypeInfo.ClrType.GetPropertyIgnoreCase(edmProperty.Name);
                if (clrProperty == null) //shadow property
                    continue;

                var expandAttribute = (Query.ExpandAttribute)clrProperty.GetCustomAttribute(typeof(Query.ExpandAttribute));
                if (expandAttribute == null || expandAttribute.ExpandType != Query.SelectExpandType.Automatic)
                    continue;

                if (expandAttribute.MaxDepth > 0 && expandAttribute.MaxDepth < level)
                    level = expandAttribute.MaxDepth;

                if (edmProperty is IEdmStructuralProperty structuralProperty)
                    selectItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(structuralProperty))));
                else if (edmProperty is IEdmNavigationProperty navigationProperty)
                {
                    IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(_edmModel, navigationProperty);
                    var segment = new NavigationPropertySegment(navigationProperty, entitySet);

                    Type childType = Parsers.OeExpressionHelper.GetCollectionItemType(clrProperty.PropertyType);
                    if (childType == null)
                        childType = clrProperty.PropertyType;

                    EntityTypeInfo childEntityTypeInfo = _entityTypeInfos[childType];
                    SelectItem[] childSelectItems = BuildExpandAttribute(childEntityTypeInfo, ref level);
                    if (childSelectItems.Length == 0)
                    {
                        var childSelectItemList = new List<SelectItem>();
                        foreach (IEdmStructuralProperty childStructuralProperty in childEntityTypeInfo.EdmType.StructuralProperties())
                            childSelectItemList.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(childStructuralProperty))));
                        childSelectItems = childSelectItemList.ToArray();
                    }

                    var selectExpandClause = new SelectExpandClause(childSelectItems, false);
                    selectItems.Add(new ExpandedNavigationSelectItem(new ODataExpandPath(segment), entitySet, selectExpandClause));
                }
                else
                    throw new InvalidOperationException("Unknown IEdmProperty type " + edmProperty.GetType());
            }

            if (selectItems.Count == 0)
                return Array.Empty<SelectItem>();

            selectItemArray = selectItems.ToArray();
            _edmModel.SetSelectItems(entityTypeInfo.EdmType, selectItemArray);
            return selectItemArray;
        }
        public void BuildPageAttribute()
        {
            foreach (EntityTypeInfo typeInfo in _entityTypeInfos.Values)
                BuildModelBoundAttribute(typeInfo);
        }
        private void BuildModelBoundAttribute(EntityTypeInfo entityTypeInfo)
        {
            var pageAttribute = (Query.PageAttribute)entityTypeInfo.ClrType.GetCustomAttribute(typeof(Query.PageAttribute));
            if (pageAttribute != null)
                _edmModel.SetModelBoundAttribute(entityTypeInfo.EdmType, pageAttribute);

            var countAttribute = (Query.CountAttribute)entityTypeInfo.ClrType.GetCustomAttribute(typeof(Query.CountAttribute));
            if (countAttribute != null)
                _edmModel.SetModelBoundAttribute(entityTypeInfo.EdmType, countAttribute);

            foreach (IEdmNavigationProperty navigationProperty in entityTypeInfo.EdmType.NavigationProperties())
                if (navigationProperty.Type.IsCollection())
                {
                    PropertyInfo clrProperty = entityTypeInfo.ClrType.GetPropertyIgnoreCase(navigationProperty.Name);

                    pageAttribute = (Query.PageAttribute)clrProperty.GetCustomAttribute(typeof(Query.PageAttribute));
                    if (pageAttribute != null)
                        _edmModel.SetModelBoundAttribute(navigationProperty, pageAttribute);

                    countAttribute = (Query.CountAttribute)clrProperty.GetCustomAttribute(typeof(Query.CountAttribute));
                    if (countAttribute != null)
                        _edmModel.SetModelBoundAttribute(navigationProperty, countAttribute);
                }
        }
    }
}
