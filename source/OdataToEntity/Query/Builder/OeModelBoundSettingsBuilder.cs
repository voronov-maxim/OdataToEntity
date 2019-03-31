using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Query.Builder
{
    public sealed class OeModelBoundSettingsBuilder
    {
        private readonly Dictionary<IEdmEntityType, OeModelBoundEntitySettings> _queryEntitySettings;
        private readonly Dictionary<IEdmNavigationProperty, OeModelBoundEntitySettings> _queryPropertySettings;

        public OeModelBoundSettingsBuilder()
        {
            _queryEntitySettings = new Dictionary<IEdmEntityType, OeModelBoundEntitySettings>();
            _queryPropertySettings = new Dictionary<IEdmNavigationProperty, OeModelBoundEntitySettings>();
        }

        public OeModelBoundProvider Build()
        {
            return new OeModelBoundProvider(_queryEntitySettings, _queryPropertySettings);
        }
        internal OeModelBoundEntitySettings GetEntitySettings(IEdmEntityType entityType)
        {
            _queryEntitySettings.TryGetValue(entityType, out OeModelBoundEntitySettings querySettings);
            return querySettings;
        }
        internal OeModelBoundEntitySettings GetEntitySettings(IEdmNavigationProperty navigationProperty)
        {
            _queryPropertySettings.TryGetValue(navigationProperty, out OeModelBoundEntitySettings querySettings);
            return querySettings;
        }
        private OeModelBoundEntitySettings GetEntitySettingsOrAdd(IEdmEntityType entityType)
        {
            if (_queryEntitySettings.TryGetValue(entityType, out OeModelBoundEntitySettings querySettings))
                return querySettings;

            querySettings = new OeModelBoundEntitySettings(entityType);
            _queryEntitySettings[entityType] = querySettings;
            return querySettings;
        }
        private OeModelBoundEntitySettings GetEntitySettingsOrAdd(IEdmNavigationProperty navigationProperty)
        {
            if (_queryPropertySettings.TryGetValue(navigationProperty, out OeModelBoundEntitySettings querySettings))
                return querySettings;

            querySettings = new OeModelBoundEntitySettings(navigationProperty);
            _queryPropertySettings[navigationProperty] = querySettings;

            return querySettings;
        }
        private OeModelBoundPropertySettings GetPropertySettingOrAdd(IEdmProperty edmProperty)
        {
            var entityType = (IEdmEntityType)edmProperty.DeclaringType;
            Dictionary<IEdmProperty, OeModelBoundPropertySettings> propertySettings = GetEntitySettingsOrAdd(entityType).Properties;
            if (!propertySettings.TryGetValue(edmProperty, out OeModelBoundPropertySettings propertySetting))
            {
                propertySetting = new OeModelBoundPropertySettings();
                propertySettings.Add(edmProperty, propertySetting);
            }
            return propertySetting;
        }
        private OeModelBoundPropertySettings GetPropertySettingOrAdd(IEdmNavigationProperty navigationProperty, IEdmProperty edmProperty)
        {
            Dictionary<IEdmProperty, OeModelBoundPropertySettings> propertySettings = GetEntitySettingsOrAdd(navigationProperty).Properties;
            if (!propertySettings.TryGetValue(edmProperty, out OeModelBoundPropertySettings propertySetting))
            {
                propertySetting = new OeModelBoundPropertySettings();
                propertySettings.Add(edmProperty, propertySetting);
            }
            return propertySetting;
        }
        public void SetCount(bool countable, IEdmEntityType entityType)
        {
            GetEntitySettingsOrAdd(entityType).Countable = countable;
        }
        public void SetCount(bool countable, IEdmNavigationProperty navigationProperty)
        {
            GetEntitySettingsOrAdd(navigationProperty).Countable = countable;
        }
        public void SetFilter(IEdmProperty edmProperty, bool filterable)
        {
            GetPropertySettingOrAdd(edmProperty).Filterable = filterable;
        }
        public void SetFilter(IEdmProperty edmProperty, bool filterable, IEdmNavigationProperty navigationProperty)
        {
            GetPropertySettingOrAdd(navigationProperty, edmProperty).Filterable = filterable;
        }
        public void SetMaxTop(int maxTop, IEdmEntityType entityType)
        {
            GetEntitySettingsOrAdd(entityType).MaxTop = maxTop;
        }
        public void SetMaxTop(int maxTop, IEdmNavigationProperty navigationProperty)
        {
            GetEntitySettingsOrAdd(navigationProperty).MaxTop = maxTop;
        }
        public void SetOrderBy(IEdmProperty edmProperty, bool orderable)
        {
            GetPropertySettingOrAdd(edmProperty).Orderable = orderable;
        }
        public void SetOrderBy(IEdmProperty edmProperty, bool orderable, IEdmNavigationProperty navigationProperty)
        {
            GetPropertySettingOrAdd(navigationProperty, edmProperty).Orderable = orderable;
        }
        public void SetPageSize(int pageSize, IEdmEntityType entityType)
        {
            GetEntitySettingsOrAdd(entityType).PageSize = pageSize;
        }
        public void SetPageSize(int pageSize, IEdmNavigationProperty navigationProperty)
        {
            GetEntitySettingsOrAdd(navigationProperty).PageSize = pageSize;
        }
        public void SetSelect(IEdmProperty edmProperty, SelectExpandType selectType)
        {
            GetPropertySettingOrAdd(edmProperty).SelectType = selectType;
        }
        public void SetSelect(IEdmProperty edmProperty, SelectExpandType selectType, IEdmNavigationProperty navigationProperty)
        {
            GetPropertySettingOrAdd(navigationProperty, edmProperty).SelectType = selectType;
        }
        public void SetSelectExpandItems(IEdmEntityType entityType, SelectItem[] selectExpandItems)
        {
            GetEntitySettingsOrAdd(entityType).SelectExpandItems = selectExpandItems;
        }
    }
}
