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
                OeModelBoundSettings? navigationSettings = _modelBoundProvider.GetSettings(navigationProperty);
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
                return IsFilterable ? nodeIn.Source.Accept(this) : nodeIn;
            }
            public override QueryNode Visit(CountNode nodeIn)
            {
                nodeIn.Source.Accept(this);
                return nodeIn;
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
            public override QueryNode Visit(SingleValueFunctionCallNode nodeIn)
            {
                foreach (QueryNode parameter in nodeIn.Parameters)
                    parameter.Accept(this);

                return nodeIn;
            }
            public override QueryNode Visit(SingleValueOpenPropertyAccessNode nodeIn)
            {
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
            OeModelBoundSettings? settings = GetSettings(entityType);
            return settings == null ? 0 : settings.PageSize;
        }
        public int GetPageSize(IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings? settings = GetSettings(navigationProperty);
            return settings == null ? 0 : settings.PageSize;
        }
        public OeModelBoundSettings? GetSettings(IEdmEntityType entityType)
        {
            _settings.TryGetValue(entityType, out OeModelBoundSettings? settings);
            return settings;
        }
        public OeModelBoundSettings? GetSettings(IEdmNavigationProperty navigationProperty)
        {
            _settings.TryGetValue(navigationProperty, out OeModelBoundSettings? settings);
            return settings;
        }
        private bool IsAllowed(IEdmProperty property, OeModelBoundSettings? entitySettings, OeModelBoundKind modelBoundKind)
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
            OeModelBoundSettings? settings = GetSettings(entityType);
            return settings == null || settings.Countable;
        }
        public bool IsCountable(IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings? settings = GetSettings(navigationProperty);
            return settings == null || settings.Countable;
        }
        public bool IsFilterable(FilterClause filterClause, IEdmEntityType entityType)
        {
            return IsFilterable(filterClause, GetSettings(entityType));
        }
        public bool IsFilterable(FilterClause filterClause, IEdmNavigationProperty navigationProperty)
        {
            return IsFilterable(filterClause, GetSettings(navigationProperty));
        }
        public bool IsFilterable(FilterClause filterClause, OeModelBoundSettings? settings)
        {
            if (settings != null)
            {
                var filterableVisitor = new FilterableVisitor(this, settings);
                filterClause.Expression.Accept(filterableVisitor);
                return filterableVisitor.IsFilterable;
            }

            return true;
        }
        public bool IsNavigationNextLink(IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings? settings = GetSettings(navigationProperty);
            return settings != null && settings.NavigationNextLink;
        }
        public bool IsOrderable(OrderByClause orderByClause, IEdmEntityType entityType)
        {
            return IsOrderable(orderByClause, GetSettings(entityType));
        }
        public bool IsOrdering(OrderByClause orderByClause, IEdmNavigationProperty navigationProperty)
        {
            return IsOrderable(orderByClause, GetSettings(navigationProperty));
        }
        public bool IsOrderable(OrderByClause orderByClause, OeModelBoundSettings? entitySettings)
        {
            while (orderByClause != null)
            {
                if (orderByClause.Expression is SingleValuePropertyAccessNode propertyNode)
                {
                    if (!IsAllowed(propertyNode.Property, entitySettings, OeModelBoundKind.OrderBy))
                        return false;

                    if (propertyNode.Source is SingleNavigationNode navigationNode)
                        for (; ; )
                        {
                            if (!IsAllowed(navigationNode.NavigationProperty, null, OeModelBoundKind.OrderBy))
                                return false;

                            if (navigationNode.Source is SingleNavigationNode navigationNode2)
                                navigationNode = navigationNode2;
                            else
                                break;
                        }

                }

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
        private bool IsSelectable(ODataPath path, OeModelBoundSettings? entitySettings)
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
            OeModelBoundSettings? settings = GetSettings(entityType);
            return top <= (settings == null || settings.MaxTop == 0 ? Int32.MaxValue : settings.MaxTop);
        }
        public bool IsTop(long top, IEdmNavigationProperty navigationProperty)
        {
            OeModelBoundSettings? settings = GetSettings(navigationProperty);
            return top <= (settings == null || settings.MaxTop == 0 ? Int32.MaxValue : settings.MaxTop);
        }
        public void Validate(IEdmModel edmModel, ODataUri odataUri)
        {
            IEdmEntityType entityType = OeEdmClrHelper.GetEntitySet(odataUri.Path).EntityType();
            if (odataUri.SkipToken == null)
            {
                var modelBoundValidator = new OeModelBoundValidator(this);
                modelBoundValidator.Validate(odataUri, entityType);

                var selectExpandBuilder = new OeSelectExpandBuilder(edmModel, this);
                odataUri.SelectAndExpand = selectExpandBuilder.Build(odataUri.SelectAndExpand, entityType);
            }
            else
            {
                var pageSelectItemBuilder = new OePageNextLinkSelectItemBuilder(this);
                odataUri.SelectAndExpand = pageSelectItemBuilder.Build(odataUri.SelectAndExpand, entityType);
            }
        }
    }
}
