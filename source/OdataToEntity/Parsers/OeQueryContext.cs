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
            private readonly Object _dataContext;
            private readonly Db.OeEntitySetAdapterCollection _entitySetAdapters;

            public SourceVisitor(Db.OeEntitySetAdapterCollection entitySetAdapters, Object dataContext)
            {
                _entitySetAdapters = entitySetAdapters;
                _dataContext = dataContext;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.IsGenericType && (node.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) || node.Type.GetGenericTypeDefinition() == typeof(IQueryable<>)))
                {
                    Db.OeEntitySetAdapter entitySetAdapter = _entitySetAdapters.FindByClrType(node.Type.GetGenericArguments()[0]);
                    IQueryable query = entitySetAdapter.GetEntitySet(_dataContext);
                    return Expression.Constant(query);
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
            IsDatabaseNullHighestValue = isDatabaseNullHighestValue;
            MetadataLevel = metadataLevel;

            var visitor = new OeQueryNodeVisitor(edmModel, Expression.Parameter(entitySetAdapter.EntityType));
            JoinBuilder = new Translators.OeJoinBuilder(visitor);

            if (pageSize > 0 || (odataUri.OrderBy != null && odataUri.Skip != null && odataUri.Top != null))
                SkipTokenNameValues = OeSkipTokenParser.CreateNameValues(edmModel, odataUri.OrderBy, odataUri.SkipToken);
        }

        public Cache.OeCacheContext CreateCacheContext()
        {
            return new Cache.OeCacheContext(this);
        }
        public Cache.OeCacheContext CreateCacheContext(IReadOnlyDictionary<ConstantNode, Cache.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            return new Cache.OeCacheContext(this, constantToParameterMapper);
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
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder, Expression source)
        {
            IEdmEntitySet entitySet = EntitySet;
            Type itemType = OeExpressionHelper.GetCollectionItemType(source.Type);
            if (!OeExpressionHelper.IsTupleType(itemType) && itemType != EntitySetAdapter.EntityType)
                entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, itemType);

            return expressionBuilder.CreateEntryFactory(entitySet);
        }
        public Expression CreateExpression(OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(JoinBuilder);

            expression = Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(EntitySetAdapter.EntityType));
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
            if (ODataUri.Apply == null)
            {
                if (ODataUri.OrderBy == null || PageSize == 0)
                {
                    expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
                    expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
                    expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
                }
                expression = expressionBuilder.ApplySelect(expression, this);
            }
            else
            {
                expression = expressionBuilder.ApplySkipToken(expression, SkipTokenNameValues, ODataUri.OrderBy, IsDatabaseNullHighestValue);
                expression = expressionBuilder.ApplyAggregation(expression, ODataUri.Apply);
                expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
                expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
                expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
            }
            expression = expressionBuilder.ApplyCount(expression, IsCountSegment);

            if (!IsCountSegment)
                EntryFactory = CreateEntryFactory(expressionBuilder, expression);
            if (SkipTokenNameValues != null)
                SkipTokenAccessors = OeSkipTokenParser.GetAccessors(expression, ODataUri.OrderBy, JoinBuilder);

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
        public static Expression TranslateSource(Db.OeEntitySetAdapterCollection entitySetAdapters, Object dataContext, Expression expression)
        {
            return new SourceVisitor(entitySetAdapters, dataContext).Visit(expression);
        }

        public IEdmModel EdmModel { get; }
        public IEdmEntitySet EntitySet { get; }
        public Db.OeEntitySetAdapter EntitySetAdapter { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public Translators.OeJoinBuilder JoinBuilder { get; }
        public bool IsCountSegment { get; }
        public bool IsDatabaseNullHighestValue { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public int PageSize { get; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public OePropertyAccessor[] SkipTokenAccessors { get; set; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; }
    }
}
