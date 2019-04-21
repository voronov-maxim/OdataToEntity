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
            private readonly Type _sourceType;

            public FilterVisitor(Type sourceType)
            {
                _sourceType = sourceType;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (OeExpressionHelper.GetCollectionItemTypeOrNull(node.Type) == _sourceType)
                    Source = node;

                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name == nameof(Enumerable.Take))
                    return base.Visit(node.Arguments[0]);

                var e = (MethodCallExpression)base.VisitMethodCall(node);
                if (e.Method.Name == nameof(Enumerable.Where))
                {
                    if (e.Method.GetGenericArguments()[0] == _sourceType)
                        WhereExpression = e;
                }
                else if (e.Method.Name == nameof(Enumerable.SelectMany))
                {
                    Type[] types = e.Method.GetGenericArguments();
                    if (types[types.Length - 1] == _sourceType)
                        WhereExpression = e;
                }
                else if (e.Method.Name == nameof(Enumerable.Select))
                {
                    if (e.Method.GetGenericArguments()[1] == _sourceType)
                        WhereExpression = e;
                }

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
                    {
                        query = _queryableSource(enumerableStub.EntitySet);
                        if (query != null && query.Expression is MethodCallExpression)
                            return query.Expression;
                    }

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

        private bool _initialized;
        private int? _restCount;

        public OeQueryContext(IEdmModel edmModel, ODataUri odataUri)
        {
            EdmModel = edmModel;
            ODataUri = odataUri;

            ParseNavigationSegments = OeParseNavigationSegment.GetNavigationSegments(odataUri.Path);
            EntitySetAdapter = edmModel.GetEntitySetAdapter((odataUri.Path.FirstSegment as EntitySetSegment).EntitySet);
            var visitor = new OeQueryNodeVisitor(Expression.Parameter(EntitySetAdapter.EntityType));
            JoinBuilder = new Translators.OeJoinBuilder(visitor);
            MetadataLevel = OeMetadataLevel.Minimal;
        }
        public OeQueryContext(IEdmModel edmModel, ODataUri odataUri, Db.OeEntitySetAdapter entitySetAdapter)
        {
            EdmModel = edmModel;
            ODataUri = odataUri;
            EntitySetAdapter = entitySetAdapter;

            ParseNavigationSegments = Array.Empty<OeParseNavigationSegment>();
            MetadataLevel = OeMetadataLevel.Minimal;
        }

        public Cache.OeCacheContext CreateCacheContext()
        {
            Initialize();
            return new Cache.OeCacheContext(this);
        }
        public Cache.OeCacheContext CreateCacheContext(IReadOnlyDictionary<ConstantNode, Cache.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            return new Cache.OeCacheContext(this, constantToParameterMapper);
        }
        public MethodCallExpression CreateCountExpression(Expression source)
        {
            if (EntryFactory == null)
                return null;

            Type sourceType = EdmModel.GetClrType(EntryFactory.EntitySet);
            var filterVisitor = new FilterVisitor(sourceType);
            filterVisitor.Visit(source);

            Expression whereExpression = filterVisitor.WhereExpression;
            if (whereExpression == null)
                whereExpression = filterVisitor.Source;

            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(sourceType);
            return Expression.Call(countMethodInfo, whereExpression);
        }
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder, OePropertyAccessor[] skipTokenAccessors)
        {
            IEdmEntitySet entitySet = OeParseNavigationSegment.GetEntitySet(ParseNavigationSegments);
            if (entitySet == null)
                entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, EntitySetAdapter.EntitySetName);

            return expressionBuilder.CreateEntryFactory(entitySet, skipTokenAccessors);
        }
        public Expression CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants)
        {
            Initialize();

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

            if (ODataUri.Path.LastSegment is CountSegment)
                expression = expressionBuilder.ApplyCount(expression, true);
            else
            {
                OePropertyAccessor[] skipTokenAccessors = OeSkipTokenParser.GetAccessors(expression, ODataUri.OrderBy, JoinBuilder);
                EntryFactory = CreateEntryFactory(expressionBuilder, skipTokenAccessors);
            }

            constants = expressionBuilder.Constants;
            return expression;
        }
        public Expression CreateExpression(OeConstantToVariableVisitor constantToVariableVisitor)
        {
            Expression expression = CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants);
            return constantToVariableVisitor.Translate(expression, constants);
        }
        public IEdmEntitySet GetEntitySet()
        {
            IEdmEntitySet entitySet = OeParseNavigationSegment.GetEntitySet(ParseNavigationSegments);
            if (entitySet == null && ODataUri.Path.FirstSegment is EntitySetSegment entitySetSegment)
                entitySet = entitySetSegment.EntitySet;
            return entitySet;
        }
        private void Initialize()
        {
            if (!_initialized)
            {
                _initialized = true;

                if (MaxPageSize > 0)
                {
                    _restCount = (int?)ODataUri.Top ?? Int32.MaxValue;
                    ODataUri.Top = MaxPageSize;
                }

                SkipTokenNameValues = Array.Empty<OeSkipTokenNameValue>();
                if (!(ODataUri.Path.LastSegment is OperationSegment))
                {
                    ODataUri.OrderBy = OeSkipTokenParser.GetUniqueOrderBy(GetEntitySet(), ODataUri.OrderBy, ODataUri.Apply);
                    if (ODataUri.SkipToken == null)
                        SkipTokenNameValues = Array.Empty<OeSkipTokenNameValue>();
                    else
                        SkipTokenNameValues = OeSkipTokenParser.ParseSkipToken(EdmModel, ODataUri.OrderBy, ODataUri.SkipToken, out _restCount);
                }
            }
        }
        public bool IsQueryCount()
        {
            return ODataUri.SkipToken == null ? ODataUri.QueryCount.GetValueOrDefault() : false;
        }
        public Expression TranslateSource(Object dataContext, Expression expression)
        {
            return TranslateSource(EdmModel, dataContext, expression, QueryableSource);
        }
        internal static Expression TranslateSource(IEdmModel edmModel, Object dataContext, Expression expression, Func<IEdmEntitySet, IQueryable> queryableSource)
        {
            return new SourceVisitor(edmModel, dataContext, queryableSource).Visit(expression);
        }

        public IEdmModel EdmModel { get; }
        public Db.OeEntitySetAdapter EntitySetAdapter { get; }
        public OeEntryFactory EntryFactory { get; set; }
        public Translators.OeJoinBuilder JoinBuilder { get; }
        public bool IsDatabaseNullHighestValue => EdmModel.GetDataAdapter(EdmModel.EntityContainer).IsDatabaseNullHighestValue;
        public int MaxPageSize { get; set; }
        public OeMetadataLevel MetadataLevel { get; set; }
        public ODataUri ODataUri { get; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public Func<IEdmEntitySet, IQueryable> QueryableSource { get; set; }
        public int? RestCount => _restCount;
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; private set; }
    }
}
