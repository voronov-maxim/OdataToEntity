using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeDataAdapter
    {
        private readonly OeQueryCache _queryCache;

        public OeDataAdapter(OeQueryCache queryCache)
        {
            _queryCache = queryCache ?? new OeQueryCache { AllowCache = false };
        }

        public abstract void CloseDataContext(Object dataContext);
        protected IQueryable CreateQuery(OeParseUriContext parseUriContext, Object dataContext, OeConstantToVariableVisitor constantToParamterVisitor)
        {
            IQueryable query = parseUriContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = parseUriContext.CreateExpression(query, constantToParamterVisitor);
            return query.Provider.CreateQuery(expression);
        }
        public abstract Object CreateDataContext();
        public abstract OeEntityAsyncEnumerator ExecuteEnumerator(OeParseUriContext parseUriContext, Object dataContext, CancellationToken cancellationToken);
        public abstract TResult ExecuteScalar<TResult>(OeParseUriContext parseUriContext, Object dataContext);
        public abstract OeEntitySetAdapter GetEntitySetAdapter(String entitySetName);
        public abstract Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken);

        protected internal OeQueryCache QueryCache => _queryCache;
        public abstract OeEntitySetMetaAdapterCollection EntitySetMetaAdapters { get; }
    }
}
