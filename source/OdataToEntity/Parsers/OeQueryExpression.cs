using Microsoft.OData.Edm;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeQueryExpression
    {
        private readonly IEdmEntitySet _entitySet;
        private readonly Expression _expression;
        private IQueryable _source;

        public OeQueryExpression(IEdmModel edmModel, IEdmEntitySet entitySet, Expression expression)
        {
            EdmModel = edmModel;
            _entitySet = entitySet;
            _expression = new OeEnumerableToQuerableVisitor().Visit(expression);
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
        public IQueryable GetQuerySource(Object dataContext)
        {
            Db.OeDataAdapter dataAdapter = EdmModel.GetDataAdapter(_entitySet.Container);
            return dataAdapter.EntitySetAdapters.Find(_entitySet).GetEntitySet(dataContext);
        }
        private IQueryable GetQuerySource(IEdmEntitySet entitySet)
        {
            return entitySet == _entitySet ? _source : null;
        }

        public IEdmModel EdmModel { get; }
        public OeEntryFactory EntryFactory { get; set; }
    }
}
