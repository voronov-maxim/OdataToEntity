using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Ef6
{
    public sealed class OeEf6AsyncEnumerator : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
    {
        private readonly IDbAsyncEnumerator _asyncEnumerator;

        public OeEf6AsyncEnumerator(IDbAsyncEnumerable asyncEnumerable)
        {
            _asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();
        }

        public void Dispose()
        {
            _asyncEnumerator.Dispose();
        }
        public IAsyncEnumerator<Object> GetEnumerator()
        {
            return this;
        }
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return _asyncEnumerator.MoveNextAsync(cancellationToken);
        }

        public Object Current => _asyncEnumerator.Current;
    }
}
