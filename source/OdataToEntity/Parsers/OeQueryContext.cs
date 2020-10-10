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
            private Expression? _source;
            private readonly Type _sourceType;

            public FilterVisitor(Type sourceType)
            {
                _sourceType = sourceType;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (OeExpressionHelper.GetCollectionItemTypeOrNull(node.Type) == _sourceType)
                    _source = node;

                return node;
            }
            protected override Expression VisitExtension(Expression node)
            {
                if (OeExpressionHelper.GetCollectionItemTypeOrNull(node.Type) == _sourceType)
                    _source = node;

                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
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

            public Expression Source => _source ?? throw new InvalidOperationException("Source not found in expression");
            public MethodCallExpression? WhereExpression { get; private set; }
        }

        private sealed class SourceVisitor : ExpressionVisitor
        {
            private readonly Object? _dataContext;
            private readonly IEdmModel _edmModel;
            private readonly Func<IEdmEntitySet, IQueryable?>? _queryableSource;

            public SourceVisitor(IEdmModel edmModel, Object? dataContext, Func<IEdmEntitySet, IQueryable?>? queryableSource)
            {
                _edmModel = edmModel;
                _dataContext = dataContext;
                _queryableSource = queryableSource;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value is OeEnumerableStub enumerableStub)
                {
                    IQueryable? query = null;
                    if (_queryableSource != null)
                        query = _queryableSource(enumerableStub.EntitySet);

                    if (query == null)
                    {
                        if (_dataContext == null)
                            throw new InvalidOperationException("If function queryableSource return null dataContext must be not null");

                        Db.OeEntitySetAdapter entitySetAdapter = _edmModel.GetEntitySetAdapter(enumerableStub.EntitySet);
                        query = entitySetAdapter.GetEntitySet(_dataContext);
                    }

                    return query.Expression;
                }

                return node;
            }
        }

        private readonly Translators.OeJoinBuilder _joinBuilder;
        private bool _initialized;
        private int? _restCount;

        public OeQueryContext(IEdmModel edmModel, ODataUri odataUri)
            : this(edmModel, odataUri, edmModel.GetEntitySetAdapter(((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet))
        {
            ParseNavigationSegments = OeParseNavigationSegment.GetNavigationSegments(odataUri.Path);
        }
        public OeQueryContext(IEdmModel edmModel, ODataUri odataUri, Db.OeEntitySetAdapter entitySetAdapter)
        {
            EdmModel = edmModel;
            ODataUri = odataUri;
            EntitySetAdapter = entitySetAdapter;

            var visitor = new OeQueryNodeVisitor(Expression.Parameter(entitySetAdapter.EntityType));
            _joinBuilder = new Translators.OeJoinBuilder(visitor);
            MetadataLevel = OeMetadataLevel.Minimal;
            ParseNavigationSegments = Array.Empty<OeParseNavigationSegment>();
            SkipTokenNameValues = Array.Empty<OeSkipTokenNameValue>();
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
                throw new InvalidOperationException("Cannot get count expression for scalar result expression");

            Type sourceType = EdmModel.GetClrType(EntryFactory.EntitySet);
            var filterVisitor = new FilterVisitor(sourceType);
            filterVisitor.Visit(source);

            Expression? whereExpression = filterVisitor.WhereExpression;
            if (whereExpression == null)
                whereExpression = filterVisitor.Source;

            MethodInfo countMethodInfo = OeMethodInfoHelper.GetCountMethodInfo(sourceType);
            return Expression.Call(countMethodInfo, whereExpression);
        }
        private OeEntryFactory CreateEntryFactory(OeExpressionBuilder expressionBuilder, OePropertyAccessor[] skipTokenAccessors)
        {
            IEdmEntitySet? entitySet = OeParseNavigationSegment.GetEntitySet(ParseNavigationSegments);
            if (entitySet == null)
                entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, EntitySetAdapter.EntitySetName);

            return expressionBuilder.CreateEntryFactory(entitySet, skipTokenAccessors);
        }
        public Expression CreateExpression(out IReadOnlyDictionary<ConstantExpression, ConstantNode> constants)
        {
            Initialize();

            Expression expression;
            var expressionBuilder = new OeExpressionBuilder(_joinBuilder);

            IEdmEntitySet entitySet = OeEdmClrHelper.GetEntitySet(EdmModel, EntitySetAdapter.EntitySetName);
            expression = OeEnumerableStub.CreateEnumerableStubExpression(EntitySetAdapter.EntityType, entitySet);
            expression = expressionBuilder.ApplyNavigation(expression, ParseNavigationSegments);
            if (ODataUri.Apply == null)
            {
                expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
                expression = expressionBuilder.ApplySelect(expression, this);
            }
            else
            {
                expression = expressionBuilder.ApplyAggregation(expression, ODataUri.Apply);
                expression = expressionBuilder.ApplyFilter(expression, ODataUri.Filter);
                expression = expressionBuilder.ApplySkipToken(expression, SkipTokenNameValues, ODataUri.OrderBy, IsDatabaseNullHighestValue); //order by aggregation
                expression = expressionBuilder.ApplyOrderBy(expression, ODataUri.OrderBy);
                expression = expressionBuilder.ApplySkip(expression, ODataUri.Skip, ODataUri.Path);
                expression = expressionBuilder.ApplyTake(expression, ODataUri.Top, ODataUri.Path);
            }

            if (ODataUri.Path.LastSegment is CountSegment)
                expression = expressionBuilder.ApplyCount(expression, true);
            else
            {
                OePropertyAccessor[] skipTokenAccessors = OeSkipTokenParser.GetAccessors(expression, ODataUri.OrderBy, _joinBuilder);
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
        public OeExpressionBuilder CreateExpressionBuilder()
        {
            return new OeExpressionBuilder(_joinBuilder);
        }
        public IEdmEntitySet GetEntitySet()
        {
            IEdmEntitySet? entitySet = OeParseNavigationSegment.GetEntitySet(ParseNavigationSegments);
            if (entitySet == null)
                if (ODataUri.Path.FirstSegment is EntitySetSegment entitySetSegment)
                    entitySet = entitySetSegment.EntitySet;
                else
                    throw new InvalidOperationException("Cannot get EntitySet from ODataPath");
            return entitySet;
        }
        private void Initialize()
        {
            if (!_initialized)
            {
                _initialized = true;

                int pageSize = ODataUri.GetPageSize();
                if (pageSize > 0)
                {
                    _restCount = (int?)ODataUri.Top ?? Int32.MaxValue;
                    ODataUri.Top = pageSize;
                }

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
            return ODataUri.SkipToken == null && ODataUri.QueryCount.GetValueOrDefault();
        }
        public Expression TranslateSource(Object dataContext, Expression expression)
        {
            return TranslateSource(EdmModel, dataContext, expression, QueryableSource);
        }
        internal static Expression TranslateSource(IEdmModel edmModel, Object? dataContext, Expression expression, Func<IEdmEntitySet, IQueryable?>? queryableSource)
        {
            return new SourceVisitor(edmModel, dataContext, queryableSource).Visit(expression);
        }

        public IEdmModel EdmModel { get; }
        public Db.OeEntitySetAdapter EntitySetAdapter { get; }
        public OeEntryFactory? EntryFactory { get; set; }
        public bool IsDatabaseNullHighestValue => EdmModel.GetDataAdapter(EdmModel.EntityContainer).IsDatabaseNullHighestValue;
        public OeMetadataLevel MetadataLevel { get; set; }
        public ODataUri ODataUri { get; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public Func<IEdmEntitySet, IQueryable?>? QueryableSource { get; set; }
        public int? RestCount => _restCount;
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; private set; }
        public int? TotalCountOfItems { get; set; }
    }
}