using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public abstract class OeAsyncEnumerator : IDisposable
    {
        private sealed class AsyncEnumeratorAdapter<T> : OeAsyncEnumerator
        {
            private readonly IAsyncEnumerator<T> _enumerator;

            public AsyncEnumeratorAdapter(IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
                : base(cancellationToken)
            {
                _enumerator = enumerable.GetEnumerator();
            }

            public override void Dispose()
            {
                _enumerator.Dispose();
            }
            public override async Task<bool> MoveNextAsync()
            {
                base.CancellationToken.ThrowIfCancellationRequested();
                return await _enumerator.MoveNext();
            }

            public override Object Current => _enumerator.Current;
        }

        private sealed class EmptyAsyncEnumerator : OeAsyncEnumerator
        {
            public EmptyAsyncEnumerator() : base(CancellationToken.None)
            {
            }

            public override void Dispose() { }
            public override Task<bool> MoveNextAsync() => Task.FromResult(false);

            public override object Current => null;
        }

        private sealed class EnumeratorAdapter : OeAsyncEnumerator
        {
            private readonly IEnumerator _enumerator;

            public EnumeratorAdapter(IEnumerable enumerable, CancellationToken cancellationToken)
                : base(cancellationToken)
            {
                _enumerator = enumerable.GetEnumerator();
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

        private sealed class ScalarEnumeratorAdapter : OeAsyncEnumerator
        {
            private Object _scalarResult;
            private readonly Task<Object> _scalarTask;
            private int _state;

            public ScalarEnumeratorAdapter(Task<Object> scalarTask, CancellationToken cancellationToken)
                : base(cancellationToken)
            {
                _scalarTask = scalarTask;
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

        private static readonly OeAsyncEnumerator _empty = new EmptyAsyncEnumerator();

        public OeAsyncEnumerator(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        protected internal CancellationToken CancellationToken { get; }
        public static OeAsyncEnumerator Create(IEnumerable enumerable, CancellationToken cancellationToken)
        {
            return new EnumeratorAdapter(enumerable, cancellationToken);
        }
        public static OeAsyncEnumerator Create<T>(IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
        {
            return new AsyncEnumeratorAdapter<T>(enumerable, cancellationToken);
        }
        public static OeAsyncEnumerator Create(Object scalarResult, CancellationToken cancellationToken)
        {
            return new ScalarEnumeratorAdapter(Task.FromResult(scalarResult), cancellationToken);
        }
        public static OeAsyncEnumerator Create(Task<Object> scalarResult, CancellationToken cancellationToken)
        {
            return new ScalarEnumeratorAdapter(scalarResult, cancellationToken);
        }
        public abstract void Dispose();
        public abstract Task<bool> MoveNextAsync();

        public int? Count { get; set; }
        public abstract Object Current { get; }
        public static OeAsyncEnumerator Empty => _empty;
    }

}
