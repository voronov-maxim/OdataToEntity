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
    public sealed class OeQueryExpression
    {
        private readonly IEdmEntitySet _entitySet;
        private readonly Expression _expression;
        private IQueryable _source;

        public OeQueryExpression(IEdmModel edmModel, String query)
        {
            EdmModel = edmModel;

            ODataUri odataUri = OeParser.ParseUri(edmModel, new Uri(query, UriKind.Relative));
            _entitySet = ((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet;

            var getParser = new OeGetParser(edmModel);
            OeQueryContext queryContext = getParser.CreateQueryContext(odataUri, 0, false, OeMetadataLevel.Minimal);
            Expression expression = queryContext.CreateExpression(out _);
            _expression = new OeEnumerableToQuerableVisitor().Visit(expression);

            EntryFactory = queryContext.EntryFactory;
        }
        public OeQueryExpression(IEdmModel edmModel, IEdmEntitySet entitySet, Expression expression, OeEntryFactory entryFactory = null)
        {
            EdmModel = edmModel;
            _entitySet = entitySet;
            _expression = new OeEnumerableToQuerableVisitor().Visit(expression);
            EntryFactory = entryFactory;
        }

        public IQueryable ApplyTo(IQueryable source, Object dataContext)
        {
            if (_expression == null)
                return source;

            _source = source;
            Expression expression = OeQueryContext.TranslateSource(EdmModel, dataContext, _expression, GetQuerySource);
            return source.Provider.CreateQuery(expression);
        }
        public IQueryable<T> ApplyTo<T>(IQueryable<T> source, Object dataContext)
        {
            return (IQueryable<T>)ApplyTo((IQueryable)source, dataContext);
        }
        public Expression GetExpression(Object dataContext)
        {
            _source = GetQuerySource(dataContext);
            return OeQueryContext.TranslateSource(EdmModel, dataContext, _expression, GetQuerySource);
        }
        public IQueryable GetQuerySource(Object dataContext)
        {
            Db.OeDataAdapter dataAdapter = EdmModel.GetDataAdapter(_entitySet.Container);
            return dataAdapter.EntitySetAdapters.Find(_entitySet).GetEntitySet(dataContext);
        }
        private IQueryable GetQuerySource(IEdmEntitySet entitySet)
        {
            return entitySet == _entitySet ? _source : null;
        }
        public IAsyncEnumerable<TResult> Materialize<TResult>(IQueryable result, CancellationToken cancellationToken = default)
        {
            if (EntryFactory == null)
                throw new InvalidOperationException("Must set OeEntryFactory via constructor");

            Db.OeAsyncEnumerator asyncEnumerator = Db.OeAsyncEnumerator.Create(result, cancellationToken == default ? CancellationToken.None : cancellationToken);
            return new Db.OeEntityAsyncEnumeratorAdapter<TResult>(asyncEnumerator, EntryFactory);
        }

        public IEdmModel EdmModel { get; }
        internal OeEntryFactory EntryFactory { get; }
    }
}
