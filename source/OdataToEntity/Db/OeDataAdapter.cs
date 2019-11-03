using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeDataAdapter
    {
        public OeDataAdapter(Cache.OeQueryCache? queryCache, OeOperationAdapter operationAdapter)
        {
            QueryCache = queryCache ?? new Cache.OeQueryCache(true);
            OperationAdapter = operationAdapter;
        }

        public abstract void CloseDataContext(Object dataContext);
        public abstract Object CreateDataContext();
        public abstract IAsyncEnumerable<Object> Execute(Object dataContext, OeQueryContext queryContext);
        public abstract TResult ExecuteScalar<TResult>(Object dataContext, OeQueryContext queryContext);
        public abstract Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken);
        protected internal virtual void SetEdmModel(IEdmModel edmModel) { }

        public abstract Type DataContextType { get; }
        public abstract OeEntitySetAdapterCollection EntitySetAdapters { get; }
        public bool IsCaseSensitive
        {
            get => OperationAdapter.IsCaseSensitive;
            set => OperationAdapter.IsCaseSensitive = value;
        }
        public bool IsDatabaseNullHighestValue { get; set; }
        public OeOperationAdapter OperationAdapter { get; }
        protected Cache.OeQueryCache QueryCache { get; }
    }
}
