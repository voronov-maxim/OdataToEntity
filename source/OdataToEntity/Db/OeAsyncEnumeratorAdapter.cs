using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeAsyncEnumeratorAdapter<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        private readonly OeAsyncEnumerator _asyncEnumerator;
        private readonly CancellationToken _cancellationToken;

        public OeAsyncEnumeratorAdapter(OeAsyncEnumerator asyncEnumerator, CancellationToken cancellationToken = default)
        {
            _asyncEnumerator = asyncEnumerator;
            _cancellationToken = cancellationToken;
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
            cancellationToken.ThrowIfCancellationRequested();
            return _asyncEnumerator.MoveNextAsync();
        }

        public T Current => (T)_asyncEnumerator.Current;
        public int? Count => _asyncEnumerator.Count;
    }
}
