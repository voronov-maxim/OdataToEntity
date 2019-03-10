using Microsoft.OData.Edm;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundQueryBuilder
    {
        private readonly Dictionary<IEdmEntityType, OeModelBoundQuerySettings> _queryEntitySettings;
        private readonly Dictionary<IEdmProperty, OeModelBoundQuerySettings> _queryPropertySettings;

        public OeModelBoundQueryBuilder()
        {
            _queryEntitySettings = new Dictionary<IEdmEntityType, OeModelBoundQuerySettings>();
            _queryPropertySettings = new Dictionary<IEdmProperty, OeModelBoundQuerySettings>();
        }

        public void AddOrderByDisabled(IEdmProperty edmProperty)
        {
            GetQuerySettings(edmProperty).Orderable = false;
        }
        public void AddOrderByDisabled(IEnumerable<IEdmProperty> edmProperties)
        {
            foreach (IEdmProperty edmProperty in edmProperties)
                GetQuerySettings(edmProperty).Orderable = false;
        }
        public void AddOrderByDisabled(IEdmNavigationProperty navigationProperty, IEdmProperty edmProperty)
        {
            GetQuerySettings(navigationProperty).NotOrderableCollection.Add(edmProperty);
        }
        public void AddOrderByDisabled(IEdmNavigationProperty navigationProperty, IEnumerable<IEdmProperty> edmProperties)
        {
            GetQuerySettings(navigationProperty).NotOrderableCollection.UnionWith(edmProperties);
        }
        public OeModelBoundQueryProvider Build()
        {
            return new OeModelBoundQueryProvider(_queryEntitySettings, _queryPropertySettings);
        }
        private OeModelBoundQuerySettings GetQuerySettings(IEdmEntityType entityType)
        {
            if (_queryEntitySettings.TryGetValue(entityType, out OeModelBoundQuerySettings querySettings))
                return querySettings;

            querySettings = new OeModelBoundQuerySettings();
            _queryEntitySettings[entityType] = querySettings;
            return querySettings;
        }
        private OeModelBoundQuerySettings GetQuerySettings(IEdmProperty edmProperty)
        {
            if (_queryPropertySettings.TryGetValue(edmProperty, out OeModelBoundQuerySettings querySettings))
                return querySettings;

            querySettings = new OeModelBoundQuerySettings();
            _queryPropertySettings[edmProperty] = querySettings;
            return querySettings;
        }
        public void SetCountable(IEdmEntityType entityType, bool countable)
        {
            GetQuerySettings(entityType).Countable = countable;
        }
        public void SetCountable(IEdmNavigationProperty navigationProperty, bool countable)
        {
            GetQuerySettings(navigationProperty).Countable = countable;
        }
        public void SetExpandable(IEdmNavigationProperty navigationProperty, bool expandable)
        {
            GetQuerySettings(navigationProperty).Expandable = expandable;
        }
        public void SetMaxTop(IEdmEntityType entityType, int maxTop)
        {
            if (maxTop > 0)
                GetQuerySettings(entityType).MaxTop = maxTop;
        }
        public void SetMaxTop(IEdmNavigationProperty navigationProperty, int maxTop)
        {
            if (maxTop > 0)
                GetQuerySettings(navigationProperty).MaxTop = maxTop;
        }
    }
}
