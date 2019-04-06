using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundSettingsBuilder
    {
        private readonly Dictionary<IEdmNamedElement, OeModelBoundSettings> _settings;

        public OeModelBoundSettingsBuilder()
        {
            _settings = new Dictionary<IEdmNamedElement, OeModelBoundSettings>();
        }

        public OeModelBoundProvider Build()
        {
            return new OeModelBoundProvider(_settings);
        }
        internal OeModelBoundSettings GetSettings(IEdmEntityType entityType)
        {
            _settings.TryGetValue(entityType, out OeModelBoundSettings settings);
            return settings;
        }
        internal OeModelBoundSettings GetSettings(IEdmNavigationProperty navigationProperty)
        {
            _settings.TryGetValue(navigationProperty, out OeModelBoundSettings settings);
            return settings;
        }
        private OeModelBoundSettings GetSettingsOrAdd(IEdmEntityType entityType)
        {
            if (!_settings.TryGetValue(entityType, out OeModelBoundSettings settings))
                _settings.Add(entityType, settings = new OeModelBoundSettings(entityType));
            return settings;
        }
        private OeModelBoundSettings GetSettingsOrAdd(IEdmNavigationProperty navigationProperty)
        {
            if (!_settings.TryGetValue(navigationProperty, out OeModelBoundSettings settings))
                _settings.Add(navigationProperty, settings = new OeModelBoundSettings(navigationProperty));
            return settings;
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
        public void SetSelectExpandItems(IEdmEntityType entityType, SelectItem[] selectExpandItems)
        {
            GetSettingsOrAdd(entityType).SelectExpandItems = selectExpandItems;
        }
    }
}
