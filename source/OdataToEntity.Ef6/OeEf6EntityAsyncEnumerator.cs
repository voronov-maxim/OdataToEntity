using OdataToEntity.Db;
using System;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Ef6
{
    public sealed class OeEf6EntityAsyncEnumerator : OeEntityAsyncEnumerator
    {
        private readonly IDbAsyncEnumerator _asyncEnumerator;
        private readonly CancellationToken _cancellationToken;

        public OeEf6EntityAsyncEnumerator(IDbAsyncEnumerator asyncEnumerator, CancellationToken cancellationToken)
        {
            _asyncEnumerator = asyncEnumerator;
            _cancellationToken = cancellationToken;
        }

        public override void Dispose()
        {
            _asyncEnumerator.Dispose();
        }
        public override Task<bool> MoveNextAsync()
        {
            return _asyncEnumerator.MoveNextAsync(_cancellationToken);
        }

        public override Object Current
        {
            get
            {
                return _asyncEnumerator.Current;
            }
        }
    }
}
