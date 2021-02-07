using OdataToEntity.Cache;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OdataToEntity.InMemory
{
    public sealed class InMemoryExecutor : IEnumerable, IEnumerator, IDisposable
    {
        private IEnumerator? _enumerator;
        private readonly SemaphoreSlim _mutex;
        private readonly String[] _parameterNames;
        private readonly Object?[] _parameters;
        private readonly Func<IEnumerable> _query;

        public InMemoryExecutor(Func<IEnumerable> query, IReadOnlyList<OeQueryCacheDbParameterValue> parameterValues, Object?[] parameters)
        {
            _query = query;
            _parameterNames = parameterValues.Select(p => p.ParameterName).ToArray();
            _parameters = parameters;

            _mutex = new SemaphoreSlim(1, 1);
        }

        public void Dispose()
        {
            _mutex.Release();
        }
        public IEnumerator GetEnumerator()
        {
            _enumerator = _query().GetEnumerator();
            return this;
        }
        public bool MoveNext()
        {
            return _enumerator!.MoveNext();
        }
        public void Reset()
        {
            _enumerator!.Reset();
        }
        public void SetDataContext(Object dataContext)
        {
            _parameters[0] = dataContext;
        }
        public void Wait()
        {
            _mutex.Wait();
        }

        public Object Current => _enumerator!.Current;
        public Object? this[String parameterName]
        {
            get => _parameters[Array.IndexOf(_parameterNames, parameterName) + 1];
            set => _parameters[Array.IndexOf(_parameterNames, parameterName) + 1] = value;
        }
    }
}
