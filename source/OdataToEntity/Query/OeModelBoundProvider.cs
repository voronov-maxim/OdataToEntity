using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Query.Builder;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    public sealed class OeModelBoundProvider
    {
        private sealed class FilterableVisitor : QueryNodeVisitor<QueryNode>
        {
            private readonly OeModelBoundProvider _modelBoundProvider;
            private readonly Stack<OeModelBoundSettings> _settings;

            public FilterableVisitor(OeModelBoundProvider modelBoundProvider, OeModelBoundSettings settings)
            {
                _modelBoundProvider = modelBoundProvider;
                _settings = new Stack<OeModelBoundSettings>();

                IsFilterable = true;
                _settings.Push(settings);
            }

            private bool Filterable(IEdmNavigationProperty navigationProperty)
            {
                return _modelBoundProvider.IsAllowed(navigationProperty, null, OeModelBoundKind.Filter);
            }
            private bool Filterable(IEdmStructuralProperty structuralProperty)
            {
                if (IsFilterable)
                    return _settings.Peek().IsAllowed(_modelBoundProvider, structuralProperty, OeModelBoundKind.Filter);

                return false;
            }
            private bool PushPropertySettings(IEdmNavigationProperty navigationProperty)
            {
                OeModelBoundSettings navigationSettings = _modelBoundProvider.GetSettings(navigationProperty);
                if (navigationSettings == null)
                    return false;

                _settings.Push(navigationSettings);
                return true;
            }
            public override QueryNode Visit(AllNode nodeIn)
            {
                return VisitLambda(nodeIn);
            }
            public override QueryNode Visit(AnyNode nodeIn)
            {
                return VisitLambda(nodeIn);
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
                nodeIn.Source.Accept(this);
                if (IsFilterable)
                {
                    bool isPushed = false;
                    if (nodeIn.Source is SingleNavigationNode navigationNode)
                        isPushed = PushPropertySettings(navigationNode.NavigationProperty);

                    IsFilterable &= Filterable((IEdmStructuralProperty)nodeIn.Property);

                    if (isPushed)
                        _settings.Pop();
                }
                return nodeIn;
            }
            private QueryNode VisitLambda(LambdaNode nodeIn)
            {
                var sourceNode = (CollectionNavigationNode)nodeIn.Source;
                IsFilterable &= Filterable(sourceNode.NavigationProperty);
                if (IsFilterable)
                {
                    bool isPushed = PushPropertySettings(sourceNode.NavigationProperty);
                    nodeIn.Body.Accept(this);

                    if (isPushed)
                        _settings.Pop();
                }

                return nodeIn;
            }

            public bool IsFilterable { get; private set; }
        }

        private readonly IReadOnlyDictionary<IEdmNamedElement, OeModelBoundSettings> _settings;

        internal OeModelBoundProvider(IReadOnlyDictionary<IEdmNamedElement, OeModelBoundSettings> settings)
        {
            _settings = settings;
        }

        public int GetPageSize(IEdmEntityType entityType)
        {
            OeModelBoundSettings settings = GetSettings(entityType);
            return settings == null ? 0 : settings.PageSize;
        }
        public int GetPageSize(IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings settings = GetSettings(navigationProperty);
            return settings == null ? 0 : settings.PageSize;
        }
        public SelectItem[] GetSelectExpandItems(IEdmEntityType entityType)
        {
            OeModelBoundSettings settings = GetSettings(entityType);
            return settings == null ? Array.Empty<SelectItem>() : settings.SelectExpandItems;
        }
        internal OeModelBoundSettings GetSettings(IEdmEntityType entityType)
        {
            _settings.TryGetValue(entityType, out OeModelBoundSettings settings);
            return settings;
        }
        private OeModelBoundSettings GetSettings(IEdmNavigationProperty navigationProperty)
        {
            _settings.TryGetValue(navigationProperty, out OeModelBoundSettings settings);
            return settings;
        }
        private bool IsAllowed(IEdmProperty property, OeModelBoundSettings entitySettings, OeModelBoundKind modelBoundKind)
        {
            if (entitySettings == null)
            {
                entitySettings = GetSettings((IEdmEntityType)property.DeclaringType);
                if (entitySettings == null)
                    return true;
            }

            return entitySettings.IsAllowed(this, property, modelBoundKind);
        }
        public bool IsCountable(IEdmEntityType entityType)
        {
            OeModelBoundSettings settings = GetSettings(entityType);
            return settings == null ? true : settings.Countable;
        }
        public bool IsCountable(IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings settings = GetSettings(navigationProperty);
            return settings == null ? true : settings.Countable;
        }
        public bool IsFilterable(FilterClause filterClause, IEdmEntityType entityType)
        {
            return IsFilterable(filterClause, GetSettings(entityType));
        }
        public bool IsFilterable(FilterClause filterClause, IEdmNavigationProperty navigationProperty)
        {
            return IsFilterable(filterClause, GetSettings(navigationProperty));
        }
        public bool IsFilterable(FilterClause filterClause, OeModelBoundSettings settings)
        {
            if (settings != null)
            {
                var filterableVisitor = new FilterableVisitor(this, settings);
                filterClause.Expression.Accept(filterableVisitor);
                return filterableVisitor.IsFilterable;
            }

            return true;
        }
        public bool IsOrderable(OrderByClause orderByClause, IEdmEntityType entityType)
        {
            return IsOrderable(orderByClause, GetSettings(entityType));
        }
        public bool IsOrdering(OrderByClause orderByClause, IEdmNavigationProperty navigationProperty)
        {
            return IsOrderable(orderByClause, GetSettings(navigationProperty));
        }
        public bool IsOrderable(OrderByClause orderByClause, OeModelBoundSettings entitySettings)
        {
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                if (!IsAllowed(propertyNode.Property, entitySettings, OeModelBoundKind.OrderBy))
                    return false;

                if (propertyNode.Source is SingleNavigationNode navigationNode)
                    do
                    {
                        if (!IsAllowed(navigationNode.NavigationProperty, null, OeModelBoundKind.OrderBy))
                            return false;
                    }
                    while ((navigationNode = navigationNode.Source as SingleNavigationNode) != null);

                orderByClause = orderByClause.ThenBy;
            }

            return true;
        }
        public bool IsSelectable(ODataPath path, IEdmEntityType entityType)
        {
            return IsSelectable(path, GetSettings(entityType));
        }
        public bool IsSelectable(ODataPath path, ExpandedNavigationSelectItem navigationSelectItem)
        {
            var segment = (NavigationPropertySegment)navigationSelectItem.PathToNavigationProperty.LastSegment;
            return IsSelectable(path, GetSettings(segment.NavigationProperty));
        }
        private bool IsSelectable(ODataPath path, OeModelBoundSettings entitySettings)
        {
            IEdmProperty property;
            if (path.LastSegment is NavigationPropertySegment navigationPropertySegment)
                property = navigationPropertySegment.NavigationProperty;
            else if (path.LastSegment is PropertySegment propertySegment)
                property = propertySegment.Property;
            else
                return false;

            return IsAllowed(property, entitySettings, OeModelBoundKind.Select);
        }
        public bool IsTop(long top, IEdmEntityType entityType)
        {
            OeModelBoundSettings settings = GetSettings(entityType);
            return top <= (settings == null ? Int32.MaxValue : settings.MaxTop);
        }
        public bool IsTop(long top, IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings settings = GetSettings(navigationProperty);
            return top <= (settings == null ? Int32.MaxValue : settings.MaxTop);
        }
        public void Validate(ODataUri odataUri, IEdmEntityType entityType)
        {
            if (odataUri.QueryCount.GetValueOrDefault() && !IsCountable(entityType))
                throw new ODataErrorException("EntityType " + entityType.Name + " not countable");

            if (odataUri.Top != null && !IsTop(odataUri.Top.GetValueOrDefault(), entityType))
                throw new ODataErrorException("EntityType " + entityType.Name + " not valid top");

            if (odataUri.Filter != null && !IsFilterable(odataUri.Filter, entityType))
                throw new ODataErrorException("Invalid filter by property");

            if (odataUri.OrderBy != null && !IsOrderable(odataUri.OrderBy, entityType))
                throw new ODataErrorException("Invalid order by property");

            if (odataUri.SelectAndExpand != null)
                foreach (SelectItem selectItem in odataUri.SelectAndExpand.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                        Validate(navigationSelectItem);
                    else if (selectItem is PathSelectItem pathSelectItem)
                        Validate(pathSelectItem, entityType);

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

            if (!IsSelectable(item.PathToNavigationProperty, item))
                throw new ODataErrorException("Navigation property " + item.PathToNavigationProperty.LastSegment.Identifier + " not expandable");

            if (item.FilterOption != null && !IsFilterable(item.FilterOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not filterable");

            if (item.OrderByOption != null && !IsOrdering(item.OrderByOption, navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not sortable");

            if (item.TopOption != null && !IsTop(item.TopOption.GetValueOrDefault(), navigationProperty))
                throw new ODataErrorException("Navigation property " + navigationProperty.Name + " not valid top");

            foreach (SelectItem selectItem in item.SelectAndExpand.SelectedItems)
                if (selectItem is ExpandedNavigationSelectItem navigationSelectItem)
                    Validate(navigationSelectItem);
                else if (selectItem is PathSelectItem pathSelectItem)
                    Validate(pathSelectItem, item);
        }
        private void Validate(PathSelectItem pathSelectItem, IEdmEntityType entityType)
        {
            if (!IsSelectable(pathSelectItem.SelectedPath, entityType))
                throw new ODataErrorException("Structural property " + pathSelectItem.SelectedPath.LastSegment.Identifier + " not selectable");
        }
        private void Validate(PathSelectItem pathSelectItem, ExpandedNavigationSelectItem navigationSelectItem)
        {
            if (!IsSelectable(pathSelectItem.SelectedPath, navigationSelectItem))
                throw new ODataErrorException("Structural property " + pathSelectItem.SelectedPath.LastSegment.Identifier + " not selectable");
        }
    }
}
