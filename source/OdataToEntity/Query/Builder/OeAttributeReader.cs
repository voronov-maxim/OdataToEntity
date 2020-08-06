using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OeAttributeReader<T> where T : Attribute
    {
        private readonly OeModelBoundKind _modelBoundKind;
        private readonly OeModelBoundSettingsBuilder _modelBoundSettingsBuilder;

        public OeAttributeReader(OeModelBoundSettingsBuilder modelBoundSettingsBuilder)
        {
            _modelBoundSettingsBuilder = modelBoundSettingsBuilder;
            _modelBoundKind = GetModelBoundKind();
        }

        public void Build(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            IEdmEntityType baseType = edmEntityType;
            do
            {
                BuildAttributes(baseType, clrEntityType);
            }
            while ((baseType = (IEdmEntityType)baseType.BaseType) != null);
        }
        private void BuildAttributes(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            IEnumerable<T> attributes = clrEntityType.GetCustomAttributes<T>();
            foreach (T attribute in attributes)
            {
                if (GetConfigurations(attribute).Count == 0)
                    foreach (IEdmProperty edmProperty in edmEntityType.Properties())
                        SetProperty(edmProperty, IsAllowed(attribute));
                else
                    foreach (KeyValuePair<String, SelectExpandType> configuration in GetConfigurations(attribute))
                    {
                        IEdmProperty edmProperty = edmEntityType.GetPropertyIgnoreCase(configuration.Key);
                        SetProperty(edmProperty, configuration.Value);
                    }
            }

            foreach (IEdmProperty edmProperty in edmEntityType.Properties())
            {
                PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCase(edmProperty);
                if (edmProperty is IEdmNavigationProperty navigationProperty)
                {
                    attributes = clrProperty.GetCustomAttributes<T>();
                    IEdmEntityType navigationEntityType = navigationProperty.ToEntityType();
                    foreach (T attribute in attributes)
                    {
                        SetProperty(navigationProperty, IsAllowed(attribute));
                        foreach (KeyValuePair<String, SelectExpandType> configuration in GetConfigurations(attribute))
                        {
                            IEdmProperty edmProperty2 = navigationEntityType.GetPropertyIgnoreCase(configuration.Key);
                            SetProperty(edmProperty2, configuration.Value, navigationProperty);
                        }
                    }
                }
                else
                {
                    var attribute = (T?)clrProperty.GetCustomAttribute(typeof(T));
                    if (attribute != null)
                        SetProperty(edmProperty, IsAllowed(attribute));
                }
            }
        }
        private static Dictionary<String, SelectExpandType> GetConfigurations(T attribure)
        {
            if (attribure is ExpandAttribute expandAttribute)
                return expandAttribute.ExpandConfigurations;

            if (attribure is FilterAttribute filterAttribute)
                return filterAttribute.FilterConfigurations;

            if (attribure is OrderByAttribute orderByAttribute)
                return orderByAttribute.OrderByConfigurations;

            if (attribure is SelectAttribute selectAttribute)
                return selectAttribute.SelectConfigurations;

            throw new InvalidOperationException("Unsupported attribute " + typeof(T).Name);
        }
        private static OeModelBoundKind GetModelBoundKind()
        {
            if (typeof(T) == typeof(ExpandAttribute))
                return OeModelBoundKind.Select;

            if (typeof(T) == typeof(FilterAttribute))
                return OeModelBoundKind.Filter;

            if (typeof(T) == typeof(OrderByAttribute))
                return OeModelBoundKind.OrderBy;

            if (typeof(T) == typeof(SelectAttribute))
                return OeModelBoundKind.Select;

            throw new InvalidOperationException("Unknown attribute " + typeof(T).Name);
        }
        private static SelectExpandType IsAllowed(T attribure)
        {
            if (attribure is ExpandAttribute expandAttribute)
                return expandAttribute.ExpandType;

            if (attribure is FilterAttribute filterAttribute)
                return filterAttribute.Disabled ? SelectExpandType.Disabled : SelectExpandType.Allowed;

            if (attribure is OrderByAttribute orderByAttribute)
                return orderByAttribute.Disabled ? SelectExpandType.Disabled : SelectExpandType.Allowed;

            if (attribure is SelectAttribute selectAttribute)
                return selectAttribute.SelectType;

            throw new InvalidOperationException("Unsupported attribute " + typeof(T).Name);
        }
        private void SetProperty(IEdmProperty edmProperty, SelectExpandType allowed)
        {
            switch (_modelBoundKind)
            {
                case OeModelBoundKind.Select:
                    _modelBoundSettingsBuilder.SetSelect(edmProperty, allowed);
                    break;
                case OeModelBoundKind.Filter:
                    _modelBoundSettingsBuilder.SetFilter(edmProperty, allowed != SelectExpandType.Disabled);
                    break;
                case OeModelBoundKind.OrderBy:
                    _modelBoundSettingsBuilder.SetOrderBy(edmProperty, allowed != SelectExpandType.Disabled);
                    break;
            }
        }
        private void SetProperty(IEdmProperty edmProperty, SelectExpandType allowed, IEdmNavigationProperty navigationProperty)
        {
            switch (_modelBoundKind)
            {
                case OeModelBoundKind.Select:
                    _modelBoundSettingsBuilder.SetSelect(edmProperty, allowed, navigationProperty);
                    break;
                case OeModelBoundKind.Filter:
                    _modelBoundSettingsBuilder.SetFilter(edmProperty, allowed != SelectExpandType.Disabled, navigationProperty);
                    break;
                case OeModelBoundKind.OrderBy:
                    _modelBoundSettingsBuilder.SetOrderBy(edmProperty, allowed != SelectExpandType.Disabled, navigationProperty);
                    break;
            }
        }
    }
}
