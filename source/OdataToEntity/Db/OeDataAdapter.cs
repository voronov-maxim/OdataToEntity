using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeDataAdapter
    {
        public OeDataAdapter(OeQueryCache queryCache, OeOperationAdapter operationAdapter)
        {
            QueryCache = queryCache ?? new OeQueryCache(true);
            OperationAdapter = operationAdapter;
        }

        public abstract void CloseDataContext(Object dataContext);
        public abstract Object CreateDataContext();
        public abstract OeAsyncEnumerator ExecuteEnumerator(Object dataContext, OeQueryContext queryContext, CancellationToken cancellationToken);
        public abstract TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext);
        public abstract Task<int> SaveChangesAsync(IEdmModel edmModel, Object dataContext, CancellationToken cancellationToken);

        protected OeQueryCache QueryCache { get; }
        public abstract OeEntitySetAdapterCollection EntitySetAdapters { get; }
        public bool IsDatabaseNullHighestValue { get; set; }
        public OeOperationAdapter OperationAdapter { get; }
    }
}
