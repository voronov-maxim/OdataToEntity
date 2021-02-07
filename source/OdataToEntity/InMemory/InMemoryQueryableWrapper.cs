using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace OdataToEntity.InMemory
{
    internal sealed class InMemoryQueryableWrapper<T> : IQueryable, IEnumerable<T>
    {
        private readonly Object?[] _parameters;
        private readonly InMemoryEntitySetAdapter _entitySetAdapter;

        public InMemoryQueryableWrapper(InMemoryEntitySetAdapter entitySetAdapter, Object?[] parameters)
        {
            _parameters = parameters;
            _entitySetAdapter = entitySetAdapter;
            ElementType = entitySetAdapter.EntityType;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (IEnumerator<T>)(_entitySetAdapter.GetSource(_parameters[0]!)!).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType { get; }
        public Expression Expression => Expression.Constant(this);
        public IQueryProvider Provider => throw new NotImplementedException();
    }
}
