using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public sealed class OeQueryContext
    {
        private sealed class FilterVisitor : ExpressionVisitor
        {
            private readonly Type _filterType;
            private readonly IQueryable _query;
            private MethodCallExpression _whereExpression;

            private FilterVisitor(IQueryable query, Type filterType)
            {
                _query = query;
                _filterType = filterType;
            }

            public static Expression Translate(IQueryable query, Expression expression, Type filterType)
            {
                var visitor = new FilterVisitor(query, filterType);
                visitor.Visit(expression);
                return visitor._whereExpression;
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.IsGenericType)
                {
                    Type[] args = node.Type.GetGenericArguments();
                    if (args.Length == 1 && args[0] == _query.ElementType)
                        return _query.Expression;
                }
                return base.VisitConstant(node);
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                var e = (MethodCallExpression)base.VisitMethodCall(node);
                if (e.Method.Name == nameof(Enumerable.Where) && e.Method.GetGenericArguments()[0] == _filterType)
                    _whereExpression = e;
                return e;
            }
        }

        private sealed class SourceVisitor : ExpressionVisitor
        {
            private readonly IQueryable _query;

            private SourceVisitor(IQueryable query)
            {
                _query = query;
            }

            public static Expression Translate(IQueryable query, Expression expression)
            {
                var visitor = new SourceVisitor(query);
                return visitor.Visit(expression);
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.IsGenericType)
                {
                    Type[] args = node.Type.GetGenericArguments();
                    if (args.Length == 1 && args[0] == _query.ElementType)
                        return _query.Expression;
                }
                return base.VisitConstant(node);
            }
        }

        private readonly IEdmModel _edmModel;
        private readonly IEdmEntitySet _entitySet;
        private Dictionary<OeEntryFactory, ExpandedNavigationSelectItem> _expandedNavigationSelectItem;
        private readonly bool _isCountSegment;
        private readonly bool _navigationNextLink;
        private readonly ODataUri _odataUri;
        private readonly int _pageSize;
        private readonly IReadOnlyList<OeParseNavigationSegment> _parseNavigationSegments;
        private readonly OeSkipTokenParser _skipTokenParser;

        public OeQueryContext(IEdmModel edmModel, ODataUri odataUri,
            IEdmEntitySet entitySet, IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments, bool isCountSegment, int pageSize, bool navigationNextLink)
        {
            _edmModel = edmModel;
            _odataUri = odataUri;
            _entitySet = entitySet;
            _parseNavigationSegments = parseNavigationSegments;
            _isCountSegment = isCountSegment;
            _pageSize = pageSize;
            _navigationNextLink = navigationNextLink;

            if (pageSize > 0) //zzz
                _skipTokenParser = CreateSkipTokenParser(EdmModel, ODataUri, ParseNavigationSegments);
        }

        public void AddExpandedNavigationSelectItem(OeEntryFactory entryFactory, ExpandedNavigationSelectItem item)
        {
            if (_expandedNavigationSelectItem == null)
                _expandedNavigationSelectItem = new Dictionary<OeEntryFactory, ExpandedNavigationSelectItem>();
            _expandedNavigationSelectItem.Add(entryFactory, item);
        }
        public OeCacheContext CreateCacheContext() => new OeCacheContext(this);
        public OeCacheContext CreateCacheContext(IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper) => new OeCacheContext(this, constantToParameterMapper);
        public Expression CreateCountExpression(IQueryable query, Expression expression)
        {
            Type filterType = EntryFactory == null ? query.ElementType : EdmModel.GetClrType(EntryFactory.EntityType);
            Expression filterExpression = ODataUri.Filter == null ? query.Expression : FilterVisitor.Translate(query, expression, filterType);

            Type sourceType = OeExpressionHelper.GetCollectionItemType(filterExpression.Type);
            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(sourceType);

            return Expression.Call(countMethodInfo, filterExpression);
        }
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder)
        {
            IEdmEntitySet entitySet = EntitySet;
            if (expressionBuilder.EntityType != EntitySetAdapter.EntityType)
            {
                String typeName = expressionBuilder.EntityType.FullName;
                Db.OeDataAdapter dataAdapter = EntitySetAdapter.DataAdapter;
                Db.OeEntitySetMetaAdapter entitySetMetaAdapter = dataAdapter.EntitySetMetaAdapters.FindByTypeName(typeName);
                if (entitySetMetaAdapter != null)
                    entitySet = EdmModel.FindDeclaredEntitySet(entitySetMetaAdapter.EntitySetName);
            }

            return expressionBuilder.CreateEntryFactory(entitySet);
        }
        public Expression CreateExpression(IQueryable query, OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(EdmModel, EntitySetAdapter.EntityType);

            expression = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(EntitySetAdapter.EntityType));
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
            expression = expressionBuilder.ApplySkipToken(expression, SkipTokenParser, ODataUri.SkipToken);
            expression = expressionBuilder.ApplyAggregation(expression, ODataUri.Apply);
            expression = expressionBuilder.ApplySelect(expression, this);
            expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
            expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
            expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
            expression = expressionBuilder.ApplyCount(expression, IsCountSegment);

            if (!IsCountSegment)
                EntryFactory = CreateEntryFactory(expressionBuilder);

            expression = constantToVariableVisitor.Translate(expression, expressionBuilder.Constants);
            if (ODataUri.QueryCount.GetValueOrDefault())
                CountExpression = CreateCountExpression(query, expression);

            return SourceVisitor.Translate(query, expression);
        }
        private static OeSkipTokenParser CreateSkipTokenParser(IEdmModel edmModel, ODataUri odataUri, IReadOnlyList<OeParseNavigationSegment> navigationSegments)
        {
            var entitySetSegment = (EntitySetSegment)odataUri.Path.FirstSegment;
            IEdmEntityType edmEntityType = entitySetSegment.EntitySet.EntityType();
            if (navigationSegments != null)
                for (int i = navigationSegments.Count - 1; i >= 0; i--)
                    if (navigationSegments[i].NavigationSegment != null)
                    {
                        edmEntityType = navigationSegments[i].NavigationSegment.NavigationSource.EntityType();
                        break;
                    }

            return odataUri.OrderBy == null ? new OeSkipTokenParser(edmModel, edmEntityType) : new OeSkipTokenParser(edmModel, edmEntityType, odataUri.OrderBy);
        }
        public IEnumerable<ExpandedNavigationSelectItem> GetExpandedNavigationSelectItems()
        {
            foreach (SelectItem selectItem in ODataUri.SelectAndExpand.SelectedItems)
                if (selectItem is ExpandedNavigationSelectItem item)
                {
                    var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationEdmProperty = segment.NavigationProperty;
                    if (navigationEdmProperty.Type.Definition is IEdmCollectionType)
                        yield return item;
                }
        }

        public Expression CountExpression { get; set; }
        public IEdmModel EdmModel => _edmModel;
        public IEdmEntitySet EntitySet => _entitySet;
        public Db.OeEntitySetAdapter EntitySetAdapter { get; set; }
        public OeEntryFactory EntryFactory { get; set; }
        public bool IsCountSegment => _isCountSegment;
        public OeMetadataLevel MetadataLevel { get; set; }
        public bool NavigationNextLink => _navigationNextLink;
        public ODataUri ODataUri => _odataUri;
        public int PageSize => _pageSize;
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments => _parseNavigationSegments;
        public OeSkipTokenParser SkipTokenParser => _skipTokenParser;
    }
}
