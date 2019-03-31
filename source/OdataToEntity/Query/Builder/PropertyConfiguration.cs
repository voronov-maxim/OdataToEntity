using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.Query.Builder
{
    public sealed class PropertyConfiguration
    {
        private readonly IEdmProperty _edmProperty;
        private readonly OeModelBoundFluentBuilder _modelBuilder;

        internal PropertyConfiguration(OeModelBoundFluentBuilder modelBuilder, IEdmProperty edmProperty)
        {
            _modelBuilder = modelBuilder;
            _edmProperty = edmProperty;
        }

        public PropertyConfiguration Count(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetCount(setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration Expand(SelectExpandType expandType)
        {
            return Select(expandType);
        }
        public PropertyConfiguration Expand(SelectExpandType expandType, params String[] propertyNames)
        {
            return Select(expandType, propertyNames);
        }
        public PropertyConfiguration Filter(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetFilter(_edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration Filter(QueryOptionSetting setting, params String[] propertyNames)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            IEdmEntityType entityType = navigationProperty.ToEntityType();
            foreach (String propertyName in propertyNames)
            {
                IEdmProperty edmProperty = entityType.GetPropertyIgnoreCase(propertyName);
                _modelBuilder.ModelBoundSettingsBuilder.SetFilter(edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            }
            return this;
        }
        public PropertyConfiguration OrderBy(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetOrderBy(_edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration OrderBy(QueryOptionSetting setting, params String[] propertyNames)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            IEdmEntityType entityType = navigationProperty.ToEntityType();
            foreach (String propertyName in propertyNames)
            {
                IEdmProperty edmProperty = entityType.GetPropertyIgnoreCase(propertyName);
                _modelBuilder.ModelBoundSettingsBuilder.SetOrderBy(edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            }
            return this;
        }
        public PropertyConfiguration Page(int? maxTopValue, int? pageSizeValue)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetMaxTop(maxTopValue.GetValueOrDefault(), navigationProperty);
            _modelBuilder.ModelBoundSettingsBuilder.SetPageSize(pageSizeValue.GetValueOrDefault(), navigationProperty);
            return this;
        }
        public PropertyConfiguration Property(String propertyName)
        {
            IEdmProperty property =  _edmProperty.DeclaringType.GetPropertyIgnoreCase(propertyName);
            return new PropertyConfiguration(_modelBuilder, property);
        }
        public PropertyConfiguration Select(SelectExpandType expandType)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetSelect(_edmProperty, expandType, navigationProperty);
            return this;
        }
        public PropertyConfiguration Select(SelectExpandType expandType, params String[] propertyNames)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            IEdmEntityType entityType = navigationProperty.ToEntityType();
            foreach (String propertyName in propertyNames)
            {
                IEdmProperty edmProperty = entityType.GetPropertyIgnoreCase(propertyName);
                _modelBuilder.ModelBoundSettingsBuilder.SetSelect(edmProperty, expandType, navigationProperty);
            }
            return this;
        }
    }
}
