using OdataToEntity.Db;
using System;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Ef6
{
    public sealed class OeEf6AsyncEnumerator : OeAsyncEnumerator
    {
        private readonly IDbAsyncEnumerator _asyncEnumerator;

        public OeEf6AsyncEnumerator(IDbAsyncEnumerator asyncEnumerator, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _asyncEnumerator = asyncEnumerator;
        }

        public override void Dispose() => _asyncEnumerator.Dispose();
        public override Task<bool> MoveNextAsync() => _asyncEnumerator.MoveNextAsync(base.CancellationToken);
        public override Object Current => _asyncEnumerator.Current;
    }
}
