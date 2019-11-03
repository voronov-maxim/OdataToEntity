using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace OdataToEntity.Query.Builder
{
    public readonly struct OeModelBoundValidator
    {
        private readonly OeModelBoundProvider _modelBoundProvider;

        public OeModelBoundValidator(OeModelBoundProvider modelBoundProvider)
        {
            _modelBoundProvider = modelBoundProvider;
        }

        private void Validate(SelectExpandClause selectExpandClause, IEdmEntityType? entityType, ExpandedNavigationSelectItem? navigationItem)
        {
            if (selectExpandClause != null)
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                {
                    if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                        Validate(navigationSelectItem);
                    else if (selectItem is PathSelectItem pathSelectItem)
                        Validate(pathSelectItem, entityType, navigationItem);
                }
        }
        public void Validate(ODataUri odataUri, IEdmEntityType entityType)
        {
            if (odataUri.QueryCount.GetValueOrDefault() && !_modelBoundProvider.IsCountable(entityType))
                throw new ODataErrorException("EntityType " + entityType.Name + " not countable");

            if (odataUri.Top != null && !_modelBoundProvider.IsTop(odataUri.Top.GetValueOrDefault(), entityType))
                throw new ODataErrorException("EntityType " + entityType.Name + " not valid top");

            if (odataUri.Filter != null && !_modelBoundProvider.IsFilterable(odataUri.Filter, entityType))
                throw new ODataErrorException("Invalid filter by property");

            if (odataUri.OrderBy != null && !_modelBoundProvider.IsOrderable(odataUri.OrderBy, entityType))
                throw new ODataErrorException("Invalid order by property");

            Validate(odataUri.SelectAndExpand, entityType, null);
        }
        private void Validate(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            if (item.CountOption.GetValueOrDefault() && !_modelBoundProvider.IsCountable(navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not countable");

            if (!_modelBoundProvider.IsSelectable(item.PathToNavigationProperty, item))
                throw new ODataErrorException("Navigation property " + item.PathToNavigationProperty.LastSegment.Identifier + " not expandable");

            if (item.FilterOption != null && !_modelBoundProvider.IsFilterable(item.FilterOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not filterable");

            if (item.OrderByOption != null && !_modelBoundProvider.IsOrdering(item.OrderByOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not sortable");

            if (item.TopOption != null && !_modelBoundProvider.IsTop(item.TopOption.GetValueOrDefault(), navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not valid top");

            Validate(item.SelectAndExpand, null, item);
        }
        private void Validate(PathSelectItem pathSelectItem, IEdmEntityType? entityType, ExpandedNavigationSelectItem? navigationItem)
        {
            bool isSelectable;
            if (navigationItem == null)
            {
                if (entityType == null)
                    throw new System.InvalidOperationException("entityType or navigationItem must be not null");

                isSelectable = _modelBoundProvider.IsSelectable(pathSelectItem.SelectedPath, entityType);
            }
            else
                isSelectable = _modelBoundProvider.IsSelectable(pathSelectItem.SelectedPath, navigationItem);

            if (!isSelectable)
                throw new ODataErrorException("Structural property " + pathSelectItem.SelectedPath.LastSegment.Identifier + " not selectable");
        }
    }
}
