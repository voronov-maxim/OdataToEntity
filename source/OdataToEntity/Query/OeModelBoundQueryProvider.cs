using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundQueryProvider
    {
        private sealed class FilterableVisitor : QueryNodeVisitor<QueryNode>
        {
            private readonly OeModelBoundQueryProvider _modelBoundQueryProvider;

            public FilterableVisitor(OeModelBoundQueryProvider modelBoundQueryProvider)
            {
                _modelBoundQueryProvider = modelBoundQueryProvider;
                IsFilterable = true;
            }

            private bool Filterable(IEdmProperty edmProperty)
            {
                if (IsFilterable)
                {
                    OeModelBoundQuerySettings modelBoundQuerySettings = _modelBoundQueryProvider.TryGetQuerySettings(edmProperty);
                    if (modelBoundQuerySettings != null && !modelBoundQuerySettings.Filterable)
                        return false;

                    return true;
                }

                return false;
            }
            public override QueryNode Visit(AllNode nodeIn)
            {
                var sourceNode = (CollectionNavigationNode)nodeIn.Source;
                IsFilterable &= Filterable(sourceNode.NavigationProperty);
                return IsFilterable ? nodeIn.Body.Accept(this) : nodeIn;
            }
            public override QueryNode Visit(AnyNode nodeIn)
            {
                var sourceNode = (CollectionNavigationNode)nodeIn.Source;
                IsFilterable &= Filterable(sourceNode.NavigationProperty);
                return IsFilterable ? nodeIn.Body.Accept(this) : nodeIn;
            }
            public override QueryNode Visit(BinaryOperatorNode nodeIn)
            {
                if (IsFilterable)
                    nodeIn.Left.Accept(this);
                if (IsFilterable)
                    nodeIn.Right.Accept(this);
                return nodeIn;
            }
            public override QueryNode Visit(CollectionNavigationNode nodeIn)
            {
                IsFilterable &= Filterable(nodeIn.NavigationProperty);
                return nodeIn;
            }
            public override QueryNode Visit(ConstantNode nodeIn)
            {
                return nodeIn;
            }
            public override QueryNode Visit(ConvertNode nodeIn)
            {
                return IsFilterable ? nodeIn.Accept(this) : nodeIn;
            }
            public override QueryNode Visit(InNode nodeIn)
            {
                return Visit((SingleValuePropertyAccessNode)nodeIn.Left);
            }
            public override QueryNode Visit(ResourceRangeVariableReferenceNode nodeIn)
            {
                if (nodeIn.RangeVariable.CollectionResourceNode is CollectionNavigationNode collectionNavigationNode)
                    return Visit(collectionNavigationNode);
                return nodeIn;
            }
            public override QueryNode Visit(SingleNavigationNode nodeIn)
            {
                IsFilterable &= Filterable(nodeIn.NavigationProperty);
                return nodeIn;
            }
            public override QueryNode Visit(SingleValuePropertyAccessNode nodeIn)
            {
                IsFilterable &= Filterable(nodeIn.Property);
                return nodeIn;
            }

            public bool IsFilterable { get; private set; }
        }

        private readonly IReadOnlyDictionary<IEdmEntityType, OeModelBoundQuerySettings> _queryEntitySettings;
        private readonly IReadOnlyDictionary<IEdmProperty, OeModelBoundQuerySettings> _queryPropertySettings;

        internal OeModelBoundQueryProvider(
            IReadOnlyDictionary<IEdmEntityType, OeModelBoundQuerySettings> queryEntitySettings,
            IReadOnlyDictionary<IEdmProperty, OeModelBoundQuerySettings> queryPropertySettings)
        {
            _queryEntitySettings = queryEntitySettings;
            _queryPropertySettings = queryPropertySettings;
        }

        public SelectItem[] GetSelectExpandItems(IEdmEntityType entityType)
        {
            if (_queryEntitySettings.TryGetValue(entityType, out OeModelBoundQuerySettings querySettings))
                return querySettings.SelectExpandItems;

            return Array.Empty<SelectItem>();
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
        public bool IsFilterable(FilterClause filterClause)
        {
            var filterableVisitor = new FilterableVisitor(this);
            filterClause.Expression.Accept(filterableVisitor);
            return filterableVisitor.IsFilterable;
        }
        public bool IsFilterable(FilterClause filterClause, IEdmNavigationProperty navigationProperty)
        {
            if (_queryPropertySettings.TryGetValue(navigationProperty, out OeModelBoundQuerySettings querySettings))
            {
                if (querySettings.Filterable)
                    return IsFilterable(filterClause);

                return false;
            }

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

            if (odataUri.Filter != null && !IsFilterable(odataUri.Filter))
                throw new ODataErrorException("Invalid filter by property");

            if (odataUri.OrderBy != null && !IsOrdering(odataUri.OrderBy))
                throw new ODataErrorException("Invalid order by property");

            if (odataUri.SelectAndExpand != null)
                foreach (SelectItem selectItem in odataUri.SelectAndExpand.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                        Validate(navigationSelectItem);
                    else if (selectItem is PathSelectItem pathSelectItem)
                        Validate(pathSelectItem);

            SelectItem[] selectExpandItems = GetSelectExpandItems(entityType);
            if (selectExpandItems.Length > 0)
            {
                if (odataUri.SelectAndExpand == null)
                    odataUri.SelectAndExpand = new SelectExpandClause(selectExpandItems, true);
                else
                {
                    var selectItemList = new List<SelectItem>(odataUri.SelectAndExpand.SelectedItems);
                    selectItemList.AddRange(selectExpandItems);
                    odataUri.SelectAndExpand = new SelectExpandClause(selectItemList, odataUri.SelectAndExpand.AllSelected);
                }
            }
        }
        private void Validate(ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

            if (item.CountOption.GetValueOrDefault() && !IsCountable(navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not countable");

            if (!IsSelectable(item.PathToNavigationProperty))
                throw new ODataErrorException("Navigation property " + item.PathToNavigationProperty.LastSegment.Identifier + " not expandable");

            if (item.FilterOption != null && !IsFilterable(item.FilterOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not filterable");

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
                throw new ODataErrorException("Structural property " + item.SelectedPath.LastSegment.Identifier + " not selectable");
        }
    }
}
