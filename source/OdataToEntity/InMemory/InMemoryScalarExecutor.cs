using OdataToEntity.Cache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.InMemory
{
    public sealed class InMemoryScalarExecutor<TResult>
    {
        private readonly String[] _parameterNames;
        private readonly Object?[] _parameters;
        private readonly Func<TResult> _query;

        public InMemoryScalarExecutor(Func<TResult> query, IReadOnlyList<OeQueryCacheDbParameterValue> parameterValues, Object?[] parameters)
        {
            _query = query;
            _parameterNames = parameterValues.Select(p => p.ParameterName).ToArray();
            _parameters = parameters;
        }

        public TResult Execute()
        {
            return _query();
        }

        public Object? this[String parameterName]
        {
            get => _parameters[Array.IndexOf(_parameterNames, parameterName)];
            set => _parameters[Array.IndexOf(_parameterNames, parameterName)] = value;
        }
    }
}
