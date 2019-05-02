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
            private readonly IAsyncEnumerator<Object> _asyncEnumerator;

            public AsyncEnumerableAdapter(IAsyncEnumerable<Object> asyncEnumerable)
            {
                _asyncEnumerator = asyncEnumerable.GetEnumerator();
            }

            public void Dispose()
            {
                _asyncEnumerator.Dispose();
            }
            public IAsyncEnumerator<T> GetEnumerator()
            {
                return this;
            }
            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return _asyncEnumerator.MoveNext(cancellationToken);
            }

            public T Current => (T)_asyncEnumerator.Current;
        }

        private sealed class EmptyAsyncEnumerator : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
        {
            public void Dispose()
            {
            }
            public IAsyncEnumerator<Object> GetEnumerator()
            {
                return this;
            }
            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }

            public object Current => null;
        }

        private sealed class EnumeratorAdapter : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
        {
            private readonly IEnumerator _enumerator;

            public EnumeratorAdapter(IEnumerable enumerable)
            {
                _enumerator = enumerable.GetEnumerator();
            }

            public void Dispose()
            {
                if (_enumerator is IDisposable dispose)
                    dispose.Dispose();
            }
            public IAsyncEnumerator<Object> GetEnumerator()
            {
                return this;
            }
            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<bool>(_enumerator.MoveNext());
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
            }

            public void Dispose()
            {
            }
            public IAsyncEnumerator<T> GetEnumerator()
            {
                return this;
            }
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(IAsyncEnumerable<Object> asyncEnumerable)
        {
            return new AsyncEnumerableAdapter<T>(asyncEnumerable);
        }
    }
}