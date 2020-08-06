using Microsoft.OData.Edm;
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
            MaxTop = 0;
            NavigationNextLink = false;
            PageSize = 0;

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
                OeModelBoundSettings? entitySettings = modelBoundProvider.GetSettings((IEdmEntityType)property.DeclaringType);
                if (entitySettings != null && entitySettings.GetPropertySetting(property, modelBoundKind) == SelectExpandType.Disabled)
                    return false;
            }

            return true;
        }
        public SelectExpandType? GetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind)
        {
            if (_properties.TryGetValue(property, out SelectExpandType?[]? propertySettings))
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
        internal void Merge(OeModelBoundSettings settings)
        {
            if (NavigationProperty != null || settings.NavigationProperty != null)
                throw new InvalidOperationException("Only entity settings can be merged");

            Countable &= settings.Countable;
            MaxTop = Min(MaxTop, settings.MaxTop);
            PageSize = Min(PageSize, settings.PageSize);

            foreach (KeyValuePair<IEdmProperty, SelectExpandType?[]> pair in settings._properties)
                _properties.Add(pair.Key, pair.Value);

            static int Min(int value1, int value2)
            {
                return value1 == 0 || value2 == 0 ? value1 + value2 : Math.Min(value1, value2);
            }
        }
        internal void SetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind, SelectExpandType allowed)
        {
            if (!_properties.TryGetValue(property, out SelectExpandType?[]? propertySettings))
            {
                propertySettings = new SelectExpandType?[(int)OeModelBoundKind.Select + 1];
                _properties.Add(property, propertySettings);
            }

            propertySettings[(int)modelBoundKind] = allowed;
        }

        public bool Countable { get; set; }
        public int MaxTop { get; set; }
        public bool NavigationNextLink { get; set; }
        public int PageSize { get; set; }

        public IEdmEntityType? EntityType { get; }
        public IEdmNavigationProperty? NavigationProperty { get; }
    }
}
