using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreEntityAsyncEnumerator : OeEntityAsyncEnumerator
    {
        private readonly IAsyncEnumerator<Object> _asyncEnumerator;
        private readonly CancellationToken _cancellationToken;

        public OeEfCoreEntityAsyncEnumerator(IAsyncEnumerable<Object> asyncEnumerable, CancellationToken cancellationToken)
        {
            _asyncEnumerator = asyncEnumerable.GetEnumerator();
            _cancellationToken = cancellationToken;
        }

        public override void Dispose()
        {
            _asyncEnumerator.Dispose();
        }
        public override Task<bool> MoveNextAsync()
        {
            return _asyncEnumerator.MoveNext(_cancellationToken);
        }

        public override Object Current => _asyncEnumerator.Current;
    }
}
