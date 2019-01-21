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
                if ((e.Method.Name == nameof(Enumerable.Where) || e.Method.Name == nameof(Enumerable.SelectMany)))
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
            private readonly IEdmModel _edmModel;
            private readonly Func<IEdmEntitySet, IQueryable> _queryableSource;

            public SourceVisitor(IEdmModel edmModel, Object dataContext, Func<IEdmEntitySet, IQueryable> queryableSource)
            {
                _edmModel = edmModel;
                _dataContext = dataContext;
                _queryableSource = queryableSource;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value is OeEnumerableStub enumerableStub)
                {
                    IQueryable query = null;
                    if (_queryableSource != null)
                        query = _queryableSource(enumerableStub.EntitySet);

                    if (query == null)
                    {
                        Db.OeEntitySetAdapter entitySetAdapter = _edmModel.GetEntitySetAdapter(enumerableStub.EntitySet);
                        query = entitySetAdapter.GetEntitySet(_dataContext);
                    }

                    return Expression.Constant(query);
                }

                return node;
            }
        }

        internal OeQueryContext(IEdmModel edmModel, ODataUri odataUri,
            IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments,
            bool isCountSegment, int pageSize, bool navigationNextLink, OeMetadataLevel metadataLevel, Db.OeEntitySetAdapter entitySetAdapter)
        {
            EntitySetAdapter = entitySetAdapter;
            EdmModel = edmModel;
            ODataUri = odataUri;
            ParseNavigationSegments = parseNavigationSegments;
            IsCountSegment = isCountSegment;
            PageSize = pageSize;
            NavigationNextLink = navigationNextLink;
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
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder)
        {
            IEdmEntitySet entitySet;
            if (ODataUri.Path.LastSegment is OperationSegment)
            {
                entitySet = OeOperationHelper.GetEntitySet(ODataUri.Path);
                Type clrEntityType = EdmModel.GetClrType(entitySet.EntityType());
                OePropertyAccessor[] accessors = OePropertyAccessor.CreateFromType(clrEntityType, entitySet);
                return OeEntryFactory.CreateEntryFactory(entitySet, accessors);
            }

            entitySet = OeParseNavigationSegment.GetEntitySet(ParseNavigationSegments);
            if (entitySet == null)
                entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, EntitySetAdapter.EntitySetName);

            return expressionBuilder.CreateEntryFactory(entitySet);
        }
        public Expression CreateExpression(OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(JoinBuilder);

            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, EntitySetAdapter.EntitySetName);
            expression = OeEnumerableStub.CreateEnumerableStubExpression(EntitySetAdapter.EntityType, entitySet);
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
            if (ODataUri.Apply == null)
                expression = expressionBuilder.ApplySelect(expression, this);
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
                EntryFactory = CreateEntryFactory(expressionBuilder);

            if (SkipTokenNameValues != null)
                SkipTokenAccessors = OeSkipTokenParser.GetAccessors(expression, ODataUri.OrderBy, JoinBuilder);

            return constantToVariableVisitor.Translate(expression, expressionBuilder.Constants);
        }
        public Expression TranslateSource(Object dataContext, Expression expression)
        {
            return new SourceVisitor(EdmModel, dataContext, QueryableSource).Visit(expression);
        }

        public IEdmModel EdmModel { get; }
        public Db.OeEntitySetAdapter EntitySetAdapter { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public Translators.OeJoinBuilder JoinBuilder { get; }
        public bool IsCountSegment { get; }
        public bool IsDatabaseNullHighestValue => EdmModel.GetDataAdapter(EdmModel.EntityContainer).IsDatabaseNullHighestValue;
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public int PageSize { get; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public Func<IEdmEntitySet, IQueryable> QueryableSource { get; set; }
        public OePropertyAccessor[] SkipTokenAccessors { get; set; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; }
    }
}
