using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace OdataToEntity.Parsers
{
    public readonly struct OeQueryExpression
    {
        private readonly IEdmEntitySet _entitySet;
        private readonly Expression _expression;

        public OeQueryExpression(IEdmModel edmModel, String query)
        {
            EdmModel = edmModel;

            ODataUri odataUri = OeParser.ParseUri(edmModel, new Uri(query, UriKind.Relative));
            _entitySet = ((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet;

            var queryContext = new OeQueryContext(edmModel, odataUri);
            Expression expression = queryContext.CreateExpression(out _);
            _expression = OeEnumerableToQuerableVisitor.Translate(expression);

            EntryFactory = queryContext.EntryFactory;
        }
        public OeQueryExpression(IEdmModel edmModel, IEdmEntitySet entitySet, Expression expression, OeEntryFactory? entryFactory = null)
        {
            EdmModel = edmModel;
            _entitySet = entitySet;
            _expression = OeEnumerableToQuerableVisitor.Translate(expression);
            EntryFactory = entryFactory;
        }

        public IQueryable ApplyTo(IQueryable source, Object dataContext)
        {
            if (_expression == null)
                return source;

            IEdmEntitySet entitySet = _entitySet;
            Expression expression = OeQueryContext.TranslateSource(EdmModel, dataContext, _expression, e => e == entitySet ? source : null);
            return source.Provider.CreateQuery(expression);
        }
        public IQueryable<T> ApplyTo<T>(IQueryable<T> source, Object dataContext)
        {
            return (IQueryable<T>)ApplyTo((IQueryable)source, dataContext);
        }
        public Expression GetExpression(Object dataContext)
        {
            IQueryable source = GetQuerySource(dataContext);
            IEdmEntitySet entitySet = _entitySet;
            return OeQueryContext.TranslateSource(EdmModel, dataContext, _expression, e => e == entitySet ? source : null);
        }
        public static Expression GetExpression(IEdmModel edmModel, String query, Object dataContext)
        {
            return new OeQueryExpression(edmModel, query).GetExpression(dataContext);
        }
        public static Expression GetExpression(IEdmModel edmModel, String query, IQueryable source)
        {
            var queryExpression = new OeQueryExpression(edmModel, query);
            return OeQueryContext.TranslateSource(edmModel, null, queryExpression._expression, e => source);
        }
        public IQueryable GetQuerySource(Object dataContext)
        {
            Db.OeDataAdapter dataAdapter = EdmModel.GetDataAdapter(_entitySet.Container);
            return dataAdapter.EntitySetAdapters.Find(_entitySet).GetEntitySet(dataContext);
        }
        public IAsyncEnumerable<TResult> Materialize<TResult>(IQueryable result, CancellationToken cancellationToken = default)
        {
            if (EntryFactory == null)
                throw new InvalidOperationException("Must set OeEntryFactory via constructor");

            IAsyncEnumerator<Object> asyncEnumerator = Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(result).GetAsyncEnumerator(cancellationToken);
            return new Db.OeEntityAsyncEnumeratorAdapter<TResult>(asyncEnumerator, EntryFactory);
        }

        public IEdmModel EdmModel { get; }
        internal OeEntryFactory? EntryFactory { get; }
    }
}