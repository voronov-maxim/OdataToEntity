using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Query
{
    internal abstract class OeDisabledAttributeReader<T> where T : Attribute
    {
        protected abstract void AddAttributeDisabled(IEdmNavigationProperty navigationProperty, IEdmProperty edmProperty);
        protected abstract void AddAttributeDisabled(IEdmProperty edmProperty);
        public void Build(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            Dictionary<IEdmProperty, bool> attributeProperties = GetAttributes(edmEntityType, clrEntityType);
            BuildAttributes(edmEntityType, null, attributeProperties);
        }
        private void BuildAttributes(IEdmEntityType edmEntityType, IEdmNavigationProperty navigationProperty, Dictionary<IEdmProperty, bool> attributeProperties)
        {
            if (attributeProperties.Count > 0)
            {
                bool disabled = false;
                foreach (KeyValuePair<IEdmProperty, bool> attributeProperty in attributeProperties)
                    if (attributeProperty.Value)
                    {
                        disabled = true;
                        if (edmEntityType == null)
                            AddAttributeDisabled(navigationProperty, attributeProperty.Key);
                        else
                            AddAttributeDisabled(attributeProperty.Key);
                    }

                if (!disabled)
                    if (edmEntityType == null)
                    {
                        var properties = new HashSet<IEdmProperty>(navigationProperty.ToEntityType().Properties());
                        properties.ExceptWith(attributeProperties.Keys);
                        foreach (IEdmProperty property in properties)
                            AddAttributeDisabled(navigationProperty, property);
                    }
                    else
                    {
                        var properties = new HashSet<IEdmProperty>(edmEntityType.Properties());
                        properties.ExceptWith(attributeProperties.Keys);
                        foreach (IEdmProperty property in properties)
                            AddAttributeDisabled(property);
                    }
            }
        }
        private Dictionary<IEdmProperty, bool> GetAttributes(IEdmEntityType edmEntityType, Type clrEntityType)
        {
            var attributeProperties = new Dictionary<IEdmProperty, bool>();

            IEnumerable<T> attributes = clrEntityType.GetCustomAttributes<T>();
            foreach (T attribute in attributes)
            {
                if (GetConfigurations(attribute).Count == 0)
                {
                    foreach (IEdmProperty edmProperty in edmEntityType.Properties())
                        if (!attributeProperties.ContainsKey(edmProperty))
                            attributeProperties.Add(edmProperty, GetDisabled(attribute));
                }

                foreach (KeyValuePair<String, bool> configuration in GetConfigurations(attribute))
                {
                    IEdmProperty edmProperty = edmEntityType.FindProperty(configuration.Key);
                    attributeProperties[edmProperty] = !configuration.Value;
                }
            }

            foreach (IEdmProperty edmProperty in edmEntityType.Properties())
            {
                PropertyInfo clrProperty = clrEntityType.GetPropertyIgnoreCase(edmProperty);
                if (edmProperty is IEdmNavigationProperty navigationProperty && navigationProperty.Type.IsCollection())
                {
                    attributes = clrProperty.GetCustomAttributes<T>();
                    IEdmEntityType navigationEntityType = navigationProperty.ToEntityType();
                    var navigationAttributeProperties = new Dictionary<IEdmProperty, bool>();
                    foreach (T attribute in attributes)
                        foreach (KeyValuePair<String, bool> configuration in GetConfigurations(attribute))
                        {
                            IEdmProperty edmProperty2 = navigationEntityType.FindProperty(configuration.Key);
                            navigationAttributeProperties[edmProperty2] = !configuration.Value;
                        }

                    BuildAttributes(null, navigationProperty, navigationAttributeProperties);
                }
                else
                {
                    var attribute = (T)clrProperty.GetCustomAttribute(typeof(T));
                    if (attribute != null)
                        attributeProperties[edmProperty] = GetDisabled(attribute);
                }
            }

            return attributeProperties;
        }
        protected abstract Dictionary<String, bool> GetConfigurations(T attribure);
        protected abstract bool GetDisabled(T attribure);
    }

    internal sealed class OeOrderByAttributeReader : OeDisabledAttributeReader<OrderByAttribute>
    {
        private readonly OeModelBoundQueryBuilder _modelBoundQueryBuilder;

        public OeOrderByAttributeReader(OeModelBoundQueryBuilder modelBoundQueryBuilder)
        {
            _modelBoundQueryBuilder = modelBoundQueryBuilder;
        }

        protected override void AddAttributeDisabled(IEdmNavigationProperty navigationProperty, IEdmProperty edmProperty)
        {
            _modelBoundQueryBuilder.AddOrderByDisabled(navigationProperty, edmProperty);
        }
        protected override void AddAttributeDisabled(IEdmProperty edmProperty)
        {
            _modelBoundQueryBuilder.AddOrderByDisabled(edmProperty);
        }
        protected override Dictionary<String, bool> GetConfigurations(OrderByAttribute attribure)
        {
            return attribure.OrderByConfigurations;
        }
        protected override bool GetDisabled(OrderByAttribute attribure)
        {
            return attribure.Disabled;
        }
    }

    internal sealed class OeFilterAttributeReader : OeDisabledAttributeReader<FilterAttribute>
    {
        private readonly OeModelBoundQueryBuilder _modelBoundQueryBuilder;

        public OeFilterAttributeReader(OeModelBoundQueryBuilder modelBoundQueryBuilder)
        {
            _modelBoundQueryBuilder = modelBoundQueryBuilder;
        }

        protected override void AddAttributeDisabled(IEdmNavigationProperty navigationProperty, IEdmProperty edmProperty)
        {
            _modelBoundQueryBuilder.AddFilterDisabled(navigationProperty, edmProperty);
        }
        protected override void AddAttributeDisabled(IEdmProperty edmProperty)
        {
            _modelBoundQueryBuilder.AddFilterDisabled(edmProperty);
        }
        protected override Dictionary<String, bool> GetConfigurations(FilterAttribute attribure)
        {
            return attribure.FilterConfigurations;
        }
        protected override bool GetDisabled(FilterAttribute attribure)
        {
            return attribure.Disabled;
        }
    }
}
