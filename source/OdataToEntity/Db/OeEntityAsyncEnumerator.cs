using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeEntityAsyncEnumerator : IDisposable
    {
        public abstract void Dispose();
        public abstract Task<bool> MoveNextAsync();

        public int? Count { get; set; }
        public abstract Object Current
        {
            get;
        }
    }

    public sealed class OeEntityAsyncEnumeratorAdapter : OeEntityAsyncEnumerator
    {
        private readonly IEnumerator _enumerator;
        private readonly CancellationToken _cancellationToken;

        public OeEntityAsyncEnumeratorAdapter(IEnumerable enumerable, CancellationToken cancellationToken)
        {
            _enumerator = enumerable.GetEnumerator();
            _cancellationToken = cancellationToken;
        }

        public override void Dispose()
        {
            var dispose = _enumerator as IDisposable;
            if (dispose != null)
                dispose.Dispose();
        }
        public override Task<bool> MoveNextAsync()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<bool>(_enumerator.MoveNext());
        }

        public override Object Current => _enumerator.Current;
    }

}
