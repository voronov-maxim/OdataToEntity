using Microsoft.OData.Edm;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Query.Builder
{
    public sealed class PropertyConfiguration<TEntity> where TEntity : class
    {
        private readonly IEdmProperty _edmProperty;
        private readonly OeModelBoundFluentBuilder _modelBuilder;

        internal PropertyConfiguration(OeModelBoundFluentBuilder modelBuilder, IEdmProperty edmProperty)
        {
            _modelBuilder = modelBuilder;
            _edmProperty = edmProperty;
        }
        internal PropertyConfiguration(OeModelBoundFluentBuilder modelBuilder, IEdmEntityType entityType, Expression<Func<TEntity, Object>> propertyExpression)
            : this(modelBuilder, GetEdmProperty(entityType, propertyExpression))
        {
        }

        public PropertyConfiguration<TEntity> Count(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetCount(setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> Expand(SelectExpandType expandType)
        {
            return Select(expandType);
        }
        public PropertyConfiguration<TEntity> Expand(SelectExpandType expandType, params String[] propertyNames)
        {
            return Select(expandType, propertyNames);
        }
        public PropertyConfiguration<TEntity> Filter(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetFilter(_edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> Filter(QueryOptionSetting setting, params String[] propertyNames)
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
        private static IEdmProperty GetEdmProperty(IEdmStructuredType entityType, Expression<Func<TEntity, Object>> propertyExpression)
        {
            MemberExpression property;
            if (propertyExpression.Body is UnaryExpression convert)
                property = (MemberExpression)convert.Operand;
            else
                property = (MemberExpression)propertyExpression.Body;
            var propertyInfo = (PropertyInfo)property.Member;
            return entityType.GetPropertyIgnoreCase(propertyInfo.Name);
        }
        public PropertyConfiguration<TEntity> NavigationNextLink()
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetNavigationNextLink(true, navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> OrderBy(QueryOptionSetting setting)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetOrderBy(_edmProperty, setting == QueryOptionSetting.Allowed, navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> OrderBy(QueryOptionSetting setting, params String[] propertyNames)
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
        public PropertyConfiguration<TEntity> Page(int? maxTopValue, int? pageSizeValue)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetMaxTop(maxTopValue.GetValueOrDefault(), navigationProperty);
            _modelBuilder.ModelBoundSettingsBuilder.SetPageSize(pageSizeValue.GetValueOrDefault(), navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> Property(String propertyName)
        {
            IEdmProperty property = _edmProperty.DeclaringType.GetPropertyIgnoreCase(propertyName);
            return new PropertyConfiguration<TEntity>(_modelBuilder, property);
        }
        public PropertyConfiguration<TEntity> Property(Expression<Func<TEntity, Object>> propertyExpression)
        {
            return new PropertyConfiguration<TEntity>(_modelBuilder, GetEdmProperty(_edmProperty.DeclaringType, propertyExpression));
        }
        public PropertyConfiguration<TEntity> Select(SelectExpandType expandType)
        {
            var navigationProperty = (IEdmNavigationProperty)_edmProperty;
            _modelBuilder.ModelBoundSettingsBuilder.SetSelect(_edmProperty, expandType, navigationProperty);
            return this;
        }
        public PropertyConfiguration<TEntity> Select(SelectExpandType expandType, params String[] propertyNames)
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
