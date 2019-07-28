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
        private CancellationToken _cancellationToken;

        public OeEf6AsyncEnumerator(IDbAsyncEnumerable asyncEnumerable)
        {
            _asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();
        }

        public ValueTask DisposeAsync()
        {
            _asyncEnumerator.Dispose();
            return new ValueTask();
        }
        public IAsyncEnumerator<Object> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return this;
        }
        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(_asyncEnumerator.MoveNextAsync(_cancellationToken));
        }

        public Object Current => _asyncEnumerator.Current;
    }
}
