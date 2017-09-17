using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public class OeAsyncEnumerator : IDisposable
    {
        private readonly IAsyncEnumerator<Object> _asyncEnumerator;
        private readonly CancellationToken _cancellationToken;

        public OeAsyncEnumerator(IAsyncEnumerator<Object> asyncEnumerator, CancellationToken cancellationToken)
        {
            _asyncEnumerator = asyncEnumerator;
            _cancellationToken = cancellationToken;
        }

        protected CancellationToken CancellationToken => _cancellationToken;
        public virtual void Dispose() => _asyncEnumerator.Dispose();
        public virtual Task<bool> MoveNextAsync() => _asyncEnumerator.MoveNext(_cancellationToken);

        public int? Count { get; set; }
        public virtual Object Current => _asyncEnumerator.Current;
    }

    public sealed class OeAsyncEnumeratorAdapter : OeAsyncEnumerator
    {
        private readonly IEnumerator _enumerator;

        public OeAsyncEnumeratorAdapter(IEnumerable enumerable, CancellationToken cancellationToken)
            : base(null, cancellationToken)
        {
            _enumerator = enumerable.GetEnumerator();
        }

        public override void Dispose()
        {
            var dispose = _enumerator as IDisposable;
            if (dispose != null)
                dispose.Dispose();
        }
        public override Task<bool> MoveNextAsync()
        {
            base.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<bool>(_enumerator.MoveNext());
        }

        public override Object Current => _enumerator.Current;
    }

    public sealed class OeScalarAsyncEnumeratorAdapter : OeAsyncEnumerator
    {
        private Object _scalarResult;
        private readonly Task<Object> _scalarTask;
        private int _state;
        private readonly CancellationToken _cancellationToken;

        public OeScalarAsyncEnumeratorAdapter(Task<Object> scalarTask, CancellationToken cancellationToken)
            : base(null, cancellationToken)
        {
            _scalarTask = scalarTask;
            _cancellationToken = cancellationToken;
        }

        public override void Dispose() { }
        public override async Task<bool> MoveNextAsync()
        {
            base.CancellationToken.ThrowIfCancellationRequested();
            if (_state != 0)
                return false;

            _scalarResult = await _scalarTask;
            _state++;
            return true;
        }

        public override Object Current => _scalarResult;
    }
}
