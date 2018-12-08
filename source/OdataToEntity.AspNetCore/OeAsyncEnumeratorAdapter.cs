using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    internal sealed class OeAsyncEnumeratorAdapter<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _source;
        private IEnumerator<T> _sourceEnumerator;

        public OeAsyncEnumeratorAdapter(IEnumerable<T> source)
        {
            _source = source;
        }

        public void Dispose()
        {
            if (_sourceEnumerator != null)
                _sourceEnumerator.Dispose();
        }
        public IAsyncEnumerator<T> GetEnumerator()
        {
            if (_sourceEnumerator != null)
                throw new InvalidOperationException("Already iterated");

            _sourceEnumerator = _source.GetEnumerator();
            return this;
        }
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_sourceEnumerator.MoveNext());
        }

        public T Current => _sourceEnumerator.Current;
    }
}
