using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeAsyncEnumerator : IDisposable
    {
        private sealed class EmptyAsyncEnumerator : OeAsyncEnumerator
        {
            public EmptyAsyncEnumerator() : base(CancellationToken.None)
            {
            }

            public override void Dispose() { }
            public override Task<bool> MoveNextAsync() => Task.FromResult(false);

            public override object Current => null;
        }

        private static readonly OeAsyncEnumerator _empty = new EmptyAsyncEnumerator();

        public OeAsyncEnumerator(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        protected internal CancellationToken CancellationToken { get; }
        public abstract void Dispose();
        public abstract Task<bool> MoveNextAsync();

        public int? Count { get; set; }
        public abstract Object Current { get; }
        public static OeAsyncEnumerator Empty => _empty;
    }

    public sealed class OeAsyncEnumeratorAdapter : OeAsyncEnumerator
    {
        private readonly IEnumerator _enumerator;

        public OeAsyncEnumeratorAdapter(IEnumerable enumerable, CancellationToken cancellationToken)
            : this(enumerable.GetEnumerator(), cancellationToken)
        {
        }
        public OeAsyncEnumeratorAdapter(IEnumerator enumerator, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _enumerator = enumerator;
        }

        public override void Dispose()
        {
            if (_enumerator is IDisposable dispose)
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
            : base(cancellationToken)
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

            _scalarResult = await _scalarTask.ConfigureAwait(false);
            _state++;
            return true;
        }

        public override Object Current => _scalarResult;
    }
}
