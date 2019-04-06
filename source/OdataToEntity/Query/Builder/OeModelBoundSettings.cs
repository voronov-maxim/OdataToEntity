using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundSettings
    {
        private readonly Dictionary<IEdmProperty, SelectExpandType?[]> _properties;

        private OeModelBoundSettings()
        {
            Countable = true;
            MaxTop = Int32.MinValue;
            PageSize = 0;
            SelectExpandItems = Array.Empty<SelectItem>();

            _properties = new Dictionary<IEdmProperty, SelectExpandType?[]>();
        }
        public OeModelBoundSettings(IEdmEntityType entityType) : this()
        {
            EntityType = entityType;
        }
        public OeModelBoundSettings(IEdmNavigationProperty navigationProperty) : this()
        {
            NavigationProperty = navigationProperty;
        }

        public bool IsAllowed(OeModelBoundProvider modelBoundProvider, IEdmProperty property, OeModelBoundKind modelBoundKind)
        {
            SelectExpandType? propertySetting = GetPropertySetting(property, modelBoundKind);
            if (propertySetting != null)
                return propertySetting.Value != SelectExpandType.Disabled;

            if (NavigationProperty != null)
            {
                OeModelBoundSettings entitySettings = modelBoundProvider.GetSettings((IEdmEntityType)property.DeclaringType);
                if (entitySettings != null && entitySettings.GetPropertySetting(property, modelBoundKind) == SelectExpandType.Disabled)
                    return false;
            }

            return true;
        }
        public SelectExpandType? GetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind)
        {
            if (_properties.TryGetValue(property, out SelectExpandType?[] propertySettings))
                return propertySettings[(int)modelBoundKind];

            return null;
        }
        public IEnumerable<(IEdmProperty, SelectExpandType)> GetPropertySettings(OeModelBoundKind modelBoundKind)
        {
            foreach (KeyValuePair<IEdmProperty, SelectExpandType?[]> propertySetting in _properties)
            {
                SelectExpandType? allowed = propertySetting.Value[(int)modelBoundKind];
                if (allowed != null)
                    yield return (propertySetting.Key, allowed.Value);
            }
        }
        public void SetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind, SelectExpandType allowed)
        {
            if (!_properties.TryGetValue(property, out SelectExpandType?[] propertySettings))
            {
                propertySettings = new SelectExpandType?[(int)OeModelBoundKind.Select + 1];
                _properties.Add(property, propertySettings);
            }

            propertySettings[(int)modelBoundKind] = allowed;
        }

        public bool Countable { get; set; }
        public int MaxTop { get; set; }
        public int PageSize { get; set; }
        public SelectItem[] SelectExpandItems { get; set; }

        public IEdmEntityType EntityType { get; }
        public IEdmNavigationProperty NavigationProperty { get; }
    }
}
