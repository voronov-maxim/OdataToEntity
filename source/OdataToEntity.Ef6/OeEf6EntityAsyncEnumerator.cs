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

        public OeEf6EntityAsyncEnumerator(IDbAsyncEnumerator asyncEnumerator, CancellationToken cancellationToken)
            : base(null, cancellationToken)
        {
            _asyncEnumerator = asyncEnumerator;
        }

        public override void Dispose() => _asyncEnumerator.Dispose();
        public override Task<bool> MoveNextAsync() => _asyncEnumerator.MoveNextAsync(base.CancellationToken);
        public override Object Current => _asyncEnumerator.Current;
    }
}
