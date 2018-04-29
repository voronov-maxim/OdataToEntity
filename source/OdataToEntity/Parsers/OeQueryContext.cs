using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
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
            private Type _sourceType;

            protected override Expression VisitConstant(ConstantExpression node)
            {
                Type sourceType = OeExpressionHelper.GetCollectionItemType(node.Type);
                if (sourceType != null)
                {
                    Source = node;
                    _sourceType = sourceType;
                }
                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name == nameof(Enumerable.Take))
                    return base.Visit(node.Arguments[0]);

                var e = (MethodCallExpression)base.VisitMethodCall(node);
                if ((e.Method.Name == nameof(Enumerable.Where) || e.Method.Name == nameof(Enumerable.SelectMany)))// && e.Method.GetGenericArguments()[0] == _sourceType)
                    WhereExpression = e;
                return e;
            }
            protected override Expression VisitNew(NewExpression node)
            {
                return node;
            }

            public ConstantExpression Source { get; private set; }
            public MethodCallExpression WhereExpression { get; private set; }
        }

        private sealed class SourceVisitor : ExpressionVisitor
        {
            private readonly Type _elementType;
            private readonly Expression _sourceExpression;

            private SourceVisitor(Expression sourceExpression)
            {
                _sourceExpression = sourceExpression;
                _elementType = OeExpressionHelper.GetCollectionItemType(sourceExpression.Type);
            }

            public static Expression Translate(Expression query, Expression expression)
            {
                var visitor = new SourceVisitor(query);
                return visitor.Visit(expression);
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.IsGenericType)
                {
                    Type[] args = node.Type.GetGenericArguments();
                    if (args.Length == 1 && args[0] == _elementType)
                        return _sourceExpression;
                }
                return base.VisitConstant(node);
            }
        }

        internal OeQueryContext(IEdmModel edmModel, ODataUri odataUri,
            IEdmEntitySet entitySet, IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments,
            bool isCountSegment, int pageSize, bool navigationNextLink, bool isDatabaseNullHighestValue,
            OeMetadataLevel metadataLevel, Db.OeEntitySetAdapter entitySetAdapter)
        {
            EntitySetAdapter = entitySetAdapter;
            EdmModel = edmModel;
            ODataUri = odataUri;
            EntitySet = entitySet;
            ParseNavigationSegments = parseNavigationSegments;
            IsCountSegment = isCountSegment;
            PageSize = pageSize;
            NavigationNextLink = navigationNextLink;
            MetadataLevel = metadataLevel;

            if (pageSize > 0 || (odataUri.OrderBy != null && odataUri.Skip != null && odataUri.Top != null))
            {
                IEdmEntityType edmEntityType = OeGetParser.GetEntityType(odataUri.Path, parseNavigationSegments);
                SkipTokenParser = new OeSkipTokenParser(edmModel, edmEntityType, isDatabaseNullHighestValue, odataUri.OrderBy, odataUri.SkipToken);
            }
        }

        public OeCacheContext CreateCacheContext()
        {
            return new OeCacheContext(this);
        }
        public OeCacheContext CreateCacheContext(IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            return new OeCacheContext(this, constantToParameterMapper);
        }
        public static MethodCallExpression CreateCountExpression(Expression expression)
        {
            var filterVisitor = new FilterVisitor();
            filterVisitor.Visit(expression);

            Expression whereExpression = filterVisitor.WhereExpression;
            if (whereExpression == null)
                whereExpression = filterVisitor.Source;

            Type sourceType = OeExpressionHelper.GetCollectionItemType(whereExpression.Type);
            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(sourceType);
            return Expression.Call(countMethodInfo, whereExpression);
        }
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder)
        {
            IEdmEntitySet entitySet = EntitySet;
            if (expressionBuilder.EntityType != EntitySetAdapter.EntityType)
                entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, expressionBuilder.EntityType);

            return expressionBuilder.CreateEntryFactory(entitySet);
        }
        public Expression CreateExpression(OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(EdmModel, EntitySetAdapter.EntityType);

            expression = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(EntitySetAdapter.EntityType));
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
            expression = expressionBuilder.ApplySkipToken(expression, SkipTokenParser);
            expression = expressionBuilder.ApplyAggregation(expression, ODataUri.Apply);
            expression = expressionBuilder.ApplySelect(expression, this);
            expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
            expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
            expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
            expression = expressionBuilder.ApplyCount(expression, IsCountSegment);

            if (!IsCountSegment)
                EntryFactory = CreateEntryFactory(expressionBuilder);

            return constantToVariableVisitor.Translate(expression, expressionBuilder.Constants);
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
        public static Expression TranslateSource(Expression query, Expression expression) => SourceVisitor.Translate(query, expression);

        public IEdmModel EdmModel { get; }
        public IEdmEntitySet EntitySet { get; }
        public Db.OeEntitySetAdapter EntitySetAdapter { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public bool IsCountSegment { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public int PageSize { get; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public OeSkipTokenParser SkipTokenParser { get; }
    }
}
