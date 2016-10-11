using System;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeEntityAsyncEnumerator : IDisposable
    {
        public abstract void Dispose();
        public abstract Task<bool> MoveNextAsync();

        public abstract Object Current
        {
            get;
        }
    }
}
