using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OePrimitiveAsyncEnumerator<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        private readonly Db.OeAsyncEnumerator _asyncEnumerator;

        public OePrimitiveAsyncEnumerator(Db.OeAsyncEnumerator asyncEnumerator)
        {
            _asyncEnumerator = asyncEnumerator;
        }

        public void Dispose()
        {
            _asyncEnumerator.Dispose();
        }
        public IAsyncEnumerator<T> GetEnumerator()
        {
            return this;
        }
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return _asyncEnumerator.MoveNextAsync();
        }

        public T Current => (T)_asyncEnumerator.Current;
    }
}
