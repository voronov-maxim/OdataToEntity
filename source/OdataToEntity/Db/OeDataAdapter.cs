using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeDataAdapter
    {
        public abstract void CloseDataContext(Object dataContext);
        public abstract Object CreateDataContext();
        public abstract OeEntityAsyncEnumerator ExecuteEnumerator(IQueryable query, CancellationToken cancellationToken);
        public abstract OeEntitySetAdapter GetEntitySetAdapter(String entitySetName);
        public abstract Task<int> SaveChangesAsync(Object dataContext, CancellationToken cancellationToken);

        public abstract OeEntitySetMetaAdapterCollection EntitySetMetaAdapters
        {
            get;
        }
    }
}
