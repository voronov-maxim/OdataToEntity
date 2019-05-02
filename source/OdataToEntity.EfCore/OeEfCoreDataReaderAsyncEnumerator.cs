using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreDataReaderAsyncEnumerator : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
    {
        private readonly RelationalDataReader _dataReader;

        public OeEfCoreDataReaderAsyncEnumerator(RelationalDataReader dataReader)
        {
            _dataReader = dataReader;
        }

        public void Dispose()
        {
            _dataReader.Dispose();
        }
        public IAsyncEnumerator<Object> GetEnumerator()
        {
            return this;
        }
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return _dataReader.ReadAsync(cancellationToken);
        }

        public Object Current => _dataReader.DbDataReader.GetValue(0);
    }
}
