using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Query
{
    internal readonly struct ModelBoundAttributeReader
    {
        private readonly IEdmModel _edmModel;
        private readonly Dictionary<Type, EntityTypeInfo> _entityTypeInfos;
        private readonly OeModelBoundQueryBuilder _modelBoundQueryBuilder;

        public ModelBoundAttributeReader(IEdmModel edmModel, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            _edmModel = edmModel;
            _entityTypeInfos = entityTypeInfos;

            _modelBoundQueryBuilder = new OeModelBoundQueryBuilder();
        }

        private void BuildModelBoundAttribute(EntityTypeInfo entityTypeInfo)
        {
            var pageAttribute = (PageAttribute)entityTypeInfo.ClrType.GetCustomAttribute(typeof(PageAttribute));
            if (pageAttribute != null)
            {
                _edmModel.SetModelBoundAttribute(entityTypeInfo.EdmType, pageAttribute);
                _modelBoundQueryBuilder.SetMaxTop(entityTypeInfo.EdmType, pageAttribute.MaxTop);
            }

            var countAttribute = (CountAttribute)entityTypeInfo.ClrType.GetCustomAttribute(typeof(CountAttribute));
            if (countAttribute != null && countAttribute.Disabled)
                _modelBoundQueryBuilder.SetCountable(entityTypeInfo.EdmType, false);

            foreach (IEdmNavigationProperty navigationProperty in entityTypeInfo.EdmType.NavigationProperties())
                if (navigationProperty.Type.IsCollection())
                {
                    PropertyInfo clrProperty = entityTypeInfo.ClrType.GetPropertyIgnoreCase(navigationProperty.Name);

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
            foreach (EntityTypeInfo typeInfo in _entityTypeInfos.Values)
            {
                int level = 3;
                BuildSelectExpandAttribute(typeInfo, new HashSet<PropertyInfo>(), level);

                BuildModelBoundAttribute(typeInfo);

                Dictionary<IEdmProperty, bool> orderByProperties = GetOrderByAttributes(typeInfo);
                BuildOrderByAttribute(typeInfo.EdmType, null, orderByProperties);
            }

            return _modelBoundQueryBuilder.Build();
        }
        private void BuildOrderByAttribute(IEdmEntityType edmEntityType, IEdmNavigationProperty navigationProperty, Dictionary<IEdmProperty, bool> orderByProperties)
        {
            if (orderByProperties.Count > 0)
            {
                bool disabled = false;
                foreach (KeyValuePair<IEdmProperty, bool> orderByProperty in orderByProperties)
                    if (orderByProperty.Value)
                    {
                        disabled = true;
                        if (edmEntityType == null)
                            _modelBoundQueryBuilder.AddOrderByDisabled(navigationProperty, orderByProperty.Key);
                        else
                            _modelBoundQueryBuilder.AddOrderByDisabled(orderByProperty.Key);
                    }

                if (!disabled)
                    if (edmEntityType == null)
                    {
                        var properties = new HashSet<IEdmProperty>(navigationProperty.ToEntityType().Properties());
                        properties.ExceptWith(orderByProperties.Keys);
                        _modelBoundQueryBuilder.AddOrderByDisabled(navigationProperty, properties);
                    }
                    else
                    {
                        var properties = new HashSet<IEdmProperty>(edmEntityType.Properties());
                        properties.ExceptWith(orderByProperties.Keys);
                        _modelBoundQueryBuilder.AddOrderByDisabled(properties);
                    }
            }
        }
        private SelectItem[] BuildSelectExpandAttribute(EntityTypeInfo entityTypeInfo, HashSet<PropertyInfo> visited, int level)
        {
            if (level < 0)
                return Array.Empty<SelectItem>();

            var selectExpandItems = new List<SelectItem>();
            foreach (IEdmProperty edmProperty in entityTypeInfo.EdmType.Properties())
            {
                PropertyInfo clrProperty = entityTypeInfo.ClrType.GetPropertyIgnoreCase(edmProperty.Name);
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

                    Type childType = Parsers.OeExpressionHelper.GetCollectionItemType(clrProperty.PropertyType);
                    if (childType == null)
                        childType = clrProperty.PropertyType;

                    SelectItem[] childSelectExpandItems = Array.Empty<SelectItem>();
                    if (visited.Add(clrProperty))
                    {
                        childSelectExpandItems = BuildSelectExpandAttribute(_entityTypeInfos[childType], visited, level - 1);
                        visited.Remove(clrProperty);
                    }
                    else
                    {
                        if (level >= 0)
                            childSelectExpandItems = BuildSelectExpandAttribute(_entityTypeInfos[childType], visited, 0);
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
            _edmModel.SetSelectExpandItems(entityTypeInfo.EdmType, selectExpandItemArray);
            return selectExpandItemArray;
        }
        private Dictionary<IEdmProperty, bool> GetOrderByAttributes(EntityTypeInfo entityTypeInfo)
        {
            var orderByProperties = new Dictionary<IEdmProperty, bool>();

            IEnumerable<OrderByAttribute> orderByAttributes = entityTypeInfo.ClrType.GetCustomAttributes<OrderByAttribute>();
            foreach (OrderByAttribute orderByAttribute in orderByAttributes)
            {
                if (orderByAttribute.OrderByConfigurations == null)
                {
                    orderByProperties.Clear();
                    foreach (IEdmProperty edmProperty in entityTypeInfo.EdmType.Properties())
                        orderByProperties.Add(edmProperty, !orderByAttribute.Disabled);
                    break;
                }

                foreach (KeyValuePair<String, bool> orderByConfiguration in orderByAttribute.OrderByConfigurations)
                {
                    IEdmProperty edmProperty = entityTypeInfo.EdmType.FindProperty(orderByConfiguration.Key);
                    orderByProperties[edmProperty] = !orderByConfiguration.Value;
                }
            }

            foreach (IEdmProperty edmProperty in entityTypeInfo.EdmType.Properties())
            {
                PropertyInfo clrProperty = entityTypeInfo.ClrType.GetPropertyIgnoreCase(edmProperty.Name);
                if (edmProperty is IEdmNavigationProperty navigationProperty && navigationProperty.Type.IsCollection())
                {
                    orderByAttributes = clrProperty.GetCustomAttributes<OrderByAttribute>();
                    IEdmEntityType navigationEntityType = navigationProperty.ToEntityType();
                    var navigationOrderByProperties = new Dictionary<IEdmProperty, bool>();
                    foreach (OrderByAttribute orderByAttribute in orderByAttributes)
                        foreach (KeyValuePair<String, bool> orderByConfiguration in orderByAttribute.OrderByConfigurations)
                        {
                            IEdmProperty edmProperty2 = navigationEntityType.FindProperty(orderByConfiguration.Key);
                            navigationOrderByProperties[edmProperty2] = !orderByConfiguration.Value;
                        }

                    BuildOrderByAttribute(null, navigationProperty, navigationOrderByProperties);
                }
                else
                {
                    var orderByAttribute = (OrderByAttribute)entityTypeInfo.ClrType.GetCustomAttribute(typeof(OrderByAttribute));
                    if (orderByAttribute != null)
                        orderByProperties[edmProperty] = orderByAttribute.Disabled;
                }
            }

            return orderByProperties;
        }
    }
}
