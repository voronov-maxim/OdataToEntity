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
        private readonly OeOperationAdapter _operationAdapter;

        public OeDataAdapter(OeQueryCache queryCache, OeOperationAdapter operationAdapter)
        {
            _queryCache = queryCache ?? new OeQueryCache { AllowCache = false };
            _operationAdapter = operationAdapter;
        }

        public abstract void CloseDataContext(Object dataContext);
        protected static IQueryable CreateQuery(OeQueryContext queryContext, Object dataContext, OeConstantToVariableVisitor constantToVariableVisitor)
        {
            IQueryable query = queryContext.EntitySetAdapter.GetEntitySet(dataContext);
            Expression expression = queryContext.CreateExpression(query, constantToVariableVisitor);
            return query.Provider.CreateQuery(expression);
        }
        public abstract Object CreateDataContext();
        public abstract OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeQueryContext queryContext, CancellationToken cancellationToken);
        public abstract TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext);
        public abstract OeEntitySetAdapter GetEntitySetAdapter(String entitySetName);
        public abstract Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken);

        protected internal OeQueryCache QueryCache => _queryCache;
        public abstract OeEntitySetMetaAdapterCollection EntitySetMetaAdapters { get; }
        public bool IsDatabaseNullHighestValue { get; set; }
        public OeOperationAdapter OperationAdapter => _operationAdapter;
    }
}
