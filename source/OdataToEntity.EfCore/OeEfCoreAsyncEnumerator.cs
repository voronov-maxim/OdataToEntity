using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreAsyncEnumerator : Db.OeAsyncEnumerator
    {
        private readonly IAsyncEnumerator<Object> _enumerator;

        public OeEfCoreAsyncEnumerator(IAsyncEnumerator<Object> enumerator, CancellationToken cancellationToken) : base(cancellationToken)
        {
            _enumerator = enumerator;
        }

        public override void Dispose() => _enumerator.Dispose();
        public override Task<bool> MoveNextAsync() => _enumerator.MoveNext();

        public override Object Current => _enumerator.Current;
    }
}
