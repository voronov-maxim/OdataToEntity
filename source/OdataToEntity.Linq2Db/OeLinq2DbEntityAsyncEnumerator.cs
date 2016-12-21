using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Linq2Db
{
    public sealed class OeLinq2DbEntityAsyncEnumerator : OeEntityAsyncEnumerator
    {
        private readonly IEnumerator<Object> _asyncEnumerator;
        private readonly CancellationToken _cancellationToken;

        public OeLinq2DbEntityAsyncEnumerator(IEnumerator<Object> asyncEnumerator, CancellationToken cancellationToken)
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
            return Task.FromResult(_asyncEnumerator.MoveNext());
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
