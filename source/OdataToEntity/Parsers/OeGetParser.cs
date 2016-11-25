using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity
{
    public sealed class OeGetParser
    {
        private sealed class SourceVisitor : ExpressionVisitor
        {
            private readonly MethodCallExpression _fakeSource;
            private static readonly MethodInfo _fakeSourceMethodInfo = GetFakeSourceMethodInfo();
            private readonly IQueryable _query;

            public SourceVisitor(IQueryable query)
            {
                _query = query;
                _fakeSource = Expression.Call(_fakeSourceMethodInfo.MakeGenericMethod(query.ElementType));
            }

            private static IEnumerable<T> FakeSource<T>()
            {
                throw new InvalidOperationException();
            }
            private static MethodInfo GetFakeSourceMethodInfo()
            {
                Func<IEnumerable<Object>> fakeSource = FakeSource<Object>;
                return fakeSource.GetMethodInfo().GetGenericMethodDefinition();
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node == _fakeSource)
                    return _query.Expression;
                return base.VisitMethodCall(node);
            }

            public MethodCallExpression Source => _fakeSource;
        }

        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OeGetParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _model = model;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEdmEntityTypeReference entityTypeRef, KeySegment keySegment)
        {
            var range = new ResourceRangeVariable("", entityTypeRef, entitySet);
            var refNode = new ResourceRangeVariableReferenceNode("$it", range);

            var pair = keySegment.Keys.Single();
            var entityType = (IEdmEntityType)keySegment.EdmType;
            IEdmProperty property = entityType.FindProperty(pair.Key);

            var left = new SingleValuePropertyAccessNode(refNode, property);
            var right = new ConstantNode(pair.Value, ODataUriUtils.ConvertToUriLiteral(pair.Value, ODataVersion.V4));

            var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);
            return new FilterClause(node, range);
        }
        public async Task ExecuteAsync(Uri requestUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            var odataParser = new ODataUriParser(_model, _baseUri, requestUri);
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataPath odataPath = odataParser.ParsePath();
            ODataUri odataUri = odataParser.ParseUri();

            IEdmEntitySet entitySet;
            IEdmEntityTypeReference entityTypeRef = GetEdmEntityTypeRef(odataPath, out entitySet);
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.GetEntitySetAdapter(entitySet.Name);

            FilterClause filterClause = odataUri.Filter;
            IEnumerable<NavigationPropertySegment> navigationSegments = null;
            if (odataPath.LastSegment is KeySegment)
                filterClause = CreateFilterClause(entitySet, entityTypeRef, odataPath.LastSegment as KeySegment);
            else if (odataPath.LastSegment is NavigationPropertySegment)
            {
                filterClause = CreateFilterClause(entitySet, entityTypeRef, odataPath.OfType<KeySegment>().Single());
                navigationSegments = odataPath.OfType<NavigationPropertySegment>();
            }

            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                var expressionBuilder = new OeExpressionBuilder(_model, entitySetAdapter.EntityType);
                IQueryable query = entitySetAdapter.GetEntitySet(dataContext);
                var visitor = new SourceVisitor(query);
                Expression expression = visitor.Source;

                expression = expressionBuilder.ApplyFilter(expression, filterClause);
                expression = expressionBuilder.ApplyNavigation(expression, navigationSegments);
                expression = expressionBuilder.ApplyAggregation(expression, odataUri.Apply);
                expression = expressionBuilder.ApplySelect(expression, odataUri.SelectAndExpand, headers.MetadataLevel);
                expression = expressionBuilder.ApplyOrderBy(expression, odataUri.OrderBy);
                expression = expressionBuilder.ApplySkip(expression, odataUri.Skip);
                expression = expressionBuilder.ApplyTake(expression, odataUri.Top);

                if (odataUri.QueryCount.GetValueOrDefault() || odataPath.LastSegment is CountSegment)
                {
                    expression = expressionBuilder.ApplyCount(expression);
                    int count = query.Provider.Execute<int>(visitor.Visit(expression));
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(count.ToString());
                    stream.Write(buffer, 0, buffer.Length);
                    return;
                }

                if (expressionBuilder.EntityType != entitySetAdapter.EntityType)
                {
                    String typeName = expressionBuilder.EntityType.FullName;
                    Db.OeEntitySetMetaAdapter entitySetMetaAdapter = _dataAdapter.EntitySetMetaAdapters.FindByTypeName(typeName);
                    if (entitySetMetaAdapter != null)
                        entitySet = _model.FindDeclaredEntitySet(entitySetMetaAdapter.EntitySetName);
                }
                OeEntryFactory entryFactory = expressionBuilder.CreateEntryFactory(entitySet);

                query = query.Provider.CreateQuery(visitor.Visit(expression));
                using (Db.OeEntityAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteEnumerator(query, cancellationToken))
                {
                    var writers = new Writers.OeGetWriter(_baseUri, _model);
                    await writers.SerializeAsync(odataUri, entryFactory, asyncEnumerator, headers, stream).ConfigureAwait(false);
                }
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
        internal static IEdmEntityTypeReference GetEdmEntityTypeRef(ODataPath odataPath, out IEdmEntitySet entitySet)
        {
            entitySet = null;
            foreach (ODataPathSegment segment in odataPath)
            {
                var entitySegment = segment as EntitySetSegment;
                if (entitySegment != null)
                {
                    entitySet = entitySegment.EntitySet;
                    return (IEdmEntityTypeReference)((IEdmCollectionType)entitySegment.EdmType).ElementType;
                }
            }
            throw new InvalidOperationException("not supported type ODataPath");
        }

    }
}
