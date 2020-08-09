using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Infrastructure
{
    public static class AsyncEnumeratorHelper
    {
        private sealed class AsyncEnumerableAdapter<T> : IAsyncEnumerable<T>, IAsyncEnumerator<T>
        {
            private readonly IAsyncEnumerator<Object?> _asyncEnumerator;

            public AsyncEnumerableAdapter(IAsyncEnumerable<Object?> asyncEnumerable, CancellationToken cancellationToken)
            {
                _asyncEnumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);
            }

            public ValueTask DisposeAsync()
            {
                return _asyncEnumerator.DisposeAsync();
            }
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                return this;
            }
            public ValueTask<bool> MoveNextAsync()
            {
                return _asyncEnumerator.MoveNextAsync();
            }

            public T Current
            {
                get
                {
                    if (_asyncEnumerator.Current is T value)
                        return value;

                    return default!;
                }
            }
        }

        private sealed class EmptyAsyncEnumerator : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
        {
            public ValueTask DisposeAsync()
            {
                return new ValueTask();
            }
            public IAsyncEnumerator<Object> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                return this;
            }
            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(false);
            }

            public Object Current => throw new InvalidOperationException("Empty collection");
        }

        private sealed class EnumeratorAdapter : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
        {
            private readonly IEnumerator _enumerator;

            public EnumeratorAdapter(IEnumerable enumerable)
            {
                _enumerator = enumerable.GetEnumerator();
            }

            public ValueTask DisposeAsync()
            {
                if (_enumerator is IDisposable dispose)
                    dispose.Dispose();
                return new ValueTask();
            }
            public IAsyncEnumerator<Object> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                return this;
            }
            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(_enumerator.MoveNext());
            }

            public Object Current => _enumerator.Current;
        }

        private sealed class ScalarEnumeratorAdapter<T> : IAsyncEnumerable<T>, IAsyncEnumerator<T>
        {
            private T _scalarResult;
            private readonly Task<T> _scalarTask;
            private int _state;

            public ScalarEnumeratorAdapter(Task<T> scalarTask)
            {
                _scalarTask = scalarTask;
                _scalarResult = default!;
            }

            public ValueTask DisposeAsync()
            {
                return new ValueTask();
            }
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                return this;
            }
            public async ValueTask<bool> MoveNextAsync()
            {
                if (_state != 0)
                    return false;

                _scalarResult = await _scalarTask.ConfigureAwait(false);
                _state++;
                return true;
            }

            public T Current => _scalarResult;
        }

        public static readonly IAsyncEnumerable<Object> Empty = new EmptyAsyncEnumerator();

        public static IAsyncEnumerable<Object> ToAsyncEnumerable(IEnumerable enumerable)
        {
            return new EnumeratorAdapter(enumerable);
        }
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(Task<T> scalarResult)
        {
            return new ScalarEnumeratorAdapter<T>(scalarResult);
        }
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(IAsyncEnumerable<Object?> asyncEnumerable, CancellationToken cancellationToken)
        {
            return new AsyncEnumerableAdapter<T>(asyncEnumerable, cancellationToken);
        }
    }
}