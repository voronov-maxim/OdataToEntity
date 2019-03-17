using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundQueryProvider
    {
        private readonly IReadOnlyDictionary<IEdmEntityType, OeModelBoundQuerySettings> _queryEntitySettings;
        private readonly IReadOnlyDictionary<IEdmProperty, OeModelBoundQuerySettings> _queryPropertySettings;

        internal OeModelBoundQueryProvider(
            IReadOnlyDictionary<IEdmEntityType, OeModelBoundQuerySettings> queryEntitySettings,
            IReadOnlyDictionary<IEdmProperty, OeModelBoundQuerySettings> queryPropertySettings)
        {
            _queryEntitySettings = queryEntitySettings;
            _queryPropertySettings = queryPropertySettings;
        }

        public bool IsCountable(IEdmEntityType entityType)
        {
            if (_queryEntitySettings.TryGetValue(entityType, out OeModelBoundQuerySettings querySettings))
                return querySettings.Countable;

            return true;
        }
        public bool IsCountable(IEdmNavigationProperty navigationProperty)
        {
            if (_queryPropertySettings.TryGetValue(navigationProperty, out OeModelBoundQuerySettings querySettings))
                return querySettings.Countable;

            return true;
        }
        public bool IsOrdering(OrderByClause orderByClause)
        {
            while (orderByClause != null)
            {
                if (orderByClause.Expression is SingleValuePropertyAccessNode propertyNode)
                {
                    OeModelBoundQuerySettings modelBoundQuerySettings;
                    if (propertyNode.Source is SingleNavigationNode navigationNode)
                    {
                        do
                        {
                            modelBoundQuerySettings = TryGetQuerySettings(navigationNode.NavigationProperty);
                            if (modelBoundQuerySettings != null && !modelBoundQuerySettings.Orderable)
                                return false;
                        }
                        while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);
                    }

                    modelBoundQuerySettings = TryGetQuerySettings(propertyNode.Property);
                    if (modelBoundQuerySettings != null && !modelBoundQuerySettings.Orderable)
                        return false;
                }

                orderByClause = orderByClause.ThenBy;
            }

            return true;
        }
        public bool IsOrdering(OrderByClause orderByClause, IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundQuerySettings modelBoundQuerySettings = TryGetQuerySettings(navigationProperty);
            if (modelBoundQuerySettings == null)
                return true;

            while (orderByClause != null)
            {
                if (orderByClause.Expression is SingleValuePropertyAccessNode propertyNode)
                    if (modelBoundQuerySettings.NotOrderableCollection.Contains(propertyNode.Property))
                        return false;

                orderByClause = orderByClause.ThenBy;
            }

            return true;
        }
        public bool IsSelectable(ODataPath path)
        {
            IEdmProperty property;
            if (path.LastSegment is NavigationPropertySegment navigationPropertySegment)
                property = navigationPropertySegment.NavigationProperty;
            else if (path.LastSegment is PropertySegment propertySegment)
                property = propertySegment.Property;
            else
                return false;

            if (_queryPropertySettings.TryGetValue(property, out OeModelBoundQuerySettings querySettings))
                return querySettings.Selectable;

            return true;
        }
        public bool IsTop(IEdmEntityType entityType, long top)
        {
            if (_queryEntitySettings.TryGetValue(entityType, out OeModelBoundQuerySettings querySettings))
                return top <= querySettings.MaxTop;

            return true;
        }
        public bool IsTop(IEdmNavigationProperty navigationProperty, long top)
        {
            if (_queryPropertySettings.TryGetValue(navigationProperty, out OeModelBoundQuerySettings querySettings))
                return top <= querySettings.MaxTop;

            return true;
        }
        private OeModelBoundQuerySettings TryGetQuerySettings(IEdmEntityType entityType)
        {
            _queryEntitySettings.TryGetValue(entityType, out OeModelBoundQuerySettings querySettings);
            return querySettings;
        }
        private OeModelBoundQuerySettings TryGetQuerySettings(IEdmProperty edmProperty)
        {
            _queryPropertySettings.TryGetValue(edmProperty, out OeModelBoundQuerySettings querySettings);
            return querySettings;
        }
        public void Validate(ODataUri odataUri, IEdmEntityType entityType)
        {
            if (odataUri.QueryCount.GetValueOrDefault() && !IsCountable(entityType))
                throw new ODataErrorException("EntityType " + entityType.Name + " not countable");

            if (odataUri.Top != null && !IsTop(entityType, odataUri.Top.GetValueOrDefault()))
                throw new ODataErrorException("EntityType " + entityType.Name + " not valid top");

            if (!IsOrdering(odataUri.OrderBy))
                throw new ODataErrorException("Invalid order by property");

            if (odataUri.SelectAndExpand != null)
                foreach (SelectItem selectItem in odataUri.SelectAndExpand.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                        Validate(navigationSelectItem);
                    else if (selectItem is PathSelectItem pathSelectItem)
                        Validate(pathSelectItem);
        }
        private void Validate(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            if (item.CountOption.GetValueOrDefault() && !IsCountable(navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not countable");

            if (!IsSelectable(item.PathToNavigationProperty))
                throw new ODataErrorException("Navigation property " + item.PathToNavigationProperty.LastSegment.Identifier + " not expandable");

            if (item.OrderByOption != null && !IsOrdering(item.OrderByOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not sortable");

            if (item.TopOption != null && !IsTop(navigationProperty, item.TopOption.GetValueOrDefault()))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not valid top");

            foreach (SelectItem selectItem in item.SelectAndExpand.SelectedItems)
                if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                    Validate(navigationSelectItem);
                else if (selectItem is PathSelectItem pathSelectItem)
                    Validate(pathSelectItem);
        }
        private void Validate(PathSelectItem item)
        {
            if (!IsSelectable(item.SelectedPath))
                throw new ODataErrorException("Structural property " + item.SelectedPath.LastSegment.Identifier + " not expandable");
        }
    }
}
