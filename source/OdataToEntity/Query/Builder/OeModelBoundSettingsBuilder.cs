using Microsoft.OData.Edm;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundSettingsBuilder
    {
        private readonly Dictionary<IEdmNamedElement, OeModelBoundSettings> _elementSettings;

        public OeModelBoundSettingsBuilder()
        {
            _elementSettings = new Dictionary<IEdmNamedElement, OeModelBoundSettings>();
        }

        public OeModelBoundProvider Build()
        {
            foreach (KeyValuePair<IEdmNamedElement, OeModelBoundSettings> setting in _elementSettings)
                if (setting.Key is IEdmEntityType entityType)
                    MergeSettings(entityType);

            return new OeModelBoundProvider(_elementSettings);
        }
        private OeModelBoundSettings GetSettingsOrAdd(IEdmEntityType entityType)
        {
            if (!_elementSettings.TryGetValue(entityType, out OeModelBoundSettings? settings))
                _elementSettings.Add(entityType, settings = new OeModelBoundSettings(entityType));
            return settings;
        }
        private OeModelBoundSettings GetSettingsOrAdd(IEdmNavigationProperty navigationProperty)
        {
            if (!_elementSettings.TryGetValue(navigationProperty, out OeModelBoundSettings? settings))
                _elementSettings.Add(navigationProperty, settings = new OeModelBoundSettings(navigationProperty));
            return settings;
        }
        private void MergeSettings(IEdmEntityType entityType)
        {
            if (_elementSettings.TryGetValue(entityType, out OeModelBoundSettings? settings))
                while ((entityType = (IEdmEntityType)entityType.BaseType) != null)
                    if (_elementSettings.TryGetValue(entityType, out OeModelBoundSettings? baseSettings))
                        settings.Merge(baseSettings);
        }
        public void SetCount(bool countable, IEdmEntityType entityType)
        {
            GetSettingsOrAdd(entityType).Countable = countable;
        }
        public void SetCount(bool countable, IEdmNavigationProperty navigationProperty)
        {
            GetSettingsOrAdd(navigationProperty).Countable = countable;
        }
        public void SetFilter(IEdmProperty property, bool filterable)
        {
            SetPropertySetting(property, OeModelBoundKind.Filter, filterable ? SelectExpandType.Allowed : SelectExpandType.Disabled);
        }
        public void SetFilter(IEdmProperty property, bool filterable, IEdmNavigationProperty navigationProperty)
        {
            SetPropertySetting(property, OeModelBoundKind.Filter, filterable ? SelectExpandType.Allowed : SelectExpandType.Disabled, navigationProperty);
        }
        public void SetMaxTop(int maxTop, IEdmEntityType entityType)
        {
            GetSettingsOrAdd(entityType).MaxTop = maxTop;
        }
        public void SetMaxTop(int maxTop, IEdmNavigationProperty navigationProperty)
        {
            GetSettingsOrAdd(navigationProperty).MaxTop = maxTop;
        }
        public void SetNavigationNextLink(bool navigationNextLink, IEdmNavigationProperty navigationProperty)
        {
            GetSettingsOrAdd(navigationProperty).NavigationNextLink = navigationNextLink;
        }
        public void SetOrderBy(IEdmProperty property, bool orderable)
        {
            SetPropertySetting(property, OeModelBoundKind.OrderBy, orderable ? SelectExpandType.Allowed : SelectExpandType.Disabled);
        }
        public void SetOrderBy(IEdmProperty property, bool orderable, IEdmNavigationProperty navigationProperty)
        {
            SetPropertySetting(property, OeModelBoundKind.OrderBy, orderable ? SelectExpandType.Allowed : SelectExpandType.Disabled, navigationProperty);
        }
        public void SetPageSize(int pageSize, IEdmEntityType entityType)
        {
            GetSettingsOrAdd(entityType).PageSize = pageSize;
        }
        public void SetPageSize(int pageSize, IEdmNavigationProperty navigationProperty)
        {
            GetSettingsOrAdd(navigationProperty).PageSize = pageSize;
        }
        private void SetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind, SelectExpandType allowed)
        {
            var entityType = (IEdmEntityType)property.DeclaringType;
            GetSettingsOrAdd(entityType).SetPropertySetting(property, modelBoundKind, allowed);
        }
        private void SetPropertySetting(IEdmProperty property, OeModelBoundKind modelBoundKind, SelectExpandType allowed, IEdmNavigationProperty navigationProperty)
        {
            GetSettingsOrAdd(navigationProperty).SetPropertySetting(property, modelBoundKind, allowed);
        }
        public void SetSelect(IEdmProperty property, SelectExpandType selectType)
        {
            SetPropertySetting(property, OeModelBoundKind.Select, selectType);
        }
        public void SetSelect(IEdmProperty property, SelectExpandType selectType, IEdmNavigationProperty navigationProperty)
        {
            SetPropertySetting(property, OeModelBoundKind.Select, selectType, navigationProperty);
        }
    }
}
