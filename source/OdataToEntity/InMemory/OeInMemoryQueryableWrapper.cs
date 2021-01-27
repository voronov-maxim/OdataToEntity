using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;

namespace OdataToEntity.InMemory
{
    internal sealed class OeInMemoryQueryableWrapper : IQueryable
    {
        private readonly IEnumerable _source;

        public OeInMemoryQueryableWrapper(IEnumerable source, Type elementType)
        {
            _source = source;
            ElementType = elementType;
        }

        public IEnumerator GetEnumerator()
        {
            return _source.GetEnumerator();
        }

        public Type ElementType { get; }
        public Expression Expression => Expression.Constant(_source);
        public IQueryProvider Provider => throw new NotImplementedException();
    }
}
