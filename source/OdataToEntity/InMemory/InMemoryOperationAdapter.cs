using System;
using System.Collections.Generic;
using System.Threading;

namespace OdataToEntity.InMemory
{
    internal sealed class InMemoryOperationAdapter : Db.OeOperationAdapter
    {
        public static readonly Db.OeOperationAdapter Instance = new InMemoryOperationAdapter();

        private InMemoryOperationAdapter() : base(typeof(Object), false)
        {
        }

        protected override IAsyncEnumerable<Object> ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
        {
            throw new NotSupportedException();
        }
        protected override IAsyncEnumerable<Object> ExecutePrimitive(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters,
            Type returnType, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        protected override IAsyncEnumerable<Object> ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters,
            Db.OeEntitySetAdapter entitySetAdapter)
        {
            throw new NotSupportedException();
        }
        protected override String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
        {
            throw new NotSupportedException();
        }
    }
}
