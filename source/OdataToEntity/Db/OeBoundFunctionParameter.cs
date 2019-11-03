using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.Db
{
    public class OeBoundFunctionParameter
    {
        private readonly OeQueryExpression _source;
        private readonly OeQueryExpression _result;

        protected OeBoundFunctionParameter(in OeQueryExpression source, in OeQueryExpression result)
        {
            _source = source;
            _result = result;
        }

        protected IQueryable ApplyFilter(IQueryable source, Object dataContext)
        {
            return _source.ApplyTo(source, dataContext);
        }
        protected IQueryable ApplySelect(IQueryable result, Object dataContext)
        {
            return _result.ApplyTo(result, dataContext);
        }
        public OeEntryFactory CreateEntryFactoryFromTuple()
        {
            if (_result.EntryFactory == null)
                throw new InvalidOperationException("Must set OeEntryFactory via constructor OeQueryExpression");

            return _result.EntryFactory.GetEntryFactoryFromTuple(_result.EdmModel, null);
        }
        protected IAsyncEnumerable<TResult> Materialize<TResult>(IQueryable result)
        {
            return _result.Materialize<TResult>(result);
        }

        protected IEdmModel EdmModel => _source.EdmModel;
    }

    public sealed class OeBoundFunctionParameter<TSource, TResult> : OeBoundFunctionParameter
    {
        public OeBoundFunctionParameter(in OeQueryExpression source, in OeQueryExpression result)
            : base(source, result)
        {
        }

        public IQueryable<TSource> ApplyFilter(IQueryable<TSource> source, Object dataContext)
        {
            return (IQueryable<TSource>)base.ApplyFilter(source, dataContext);
        }
        public IQueryable ApplySelect(IQueryable<TResult> result, Object dataContext)
        {
            return base.ApplySelect(result, dataContext);
        }
        public void CloseDataContext<TDataContext>(TDataContext dataContext) where TDataContext : notnull
        {
            OeDataAdapter dataAdapter = base.EdmModel.GetDataAdapter(typeof(TDataContext));
            dataAdapter.CloseDataContext(dataContext);
        }
        public TDataContext CreateDataContext<TDataContext>()
        {
            OeDataAdapter dataAdapter = base.EdmModel.GetDataAdapter(typeof(TDataContext));
            return (TDataContext)dataAdapter.CreateDataContext();
        }
        public IAsyncEnumerable<TResult> Materialize(IQueryable result)
        {
            return base.Materialize<TResult>(result);
        }
    }

}
