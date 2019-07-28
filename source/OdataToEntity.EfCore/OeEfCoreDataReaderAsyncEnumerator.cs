using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreDataReaderAsyncEnumerator : IAsyncEnumerable<Object>, IAsyncEnumerator<Object>
    {
        private CancellationToken _cancellationToken;
        private readonly RelationalDataReader _dataReader;

        public OeEfCoreDataReaderAsyncEnumerator(RelationalDataReader dataReader)
        {
            _dataReader = dataReader;
        }

        public ValueTask DisposeAsync()
        {
            return _dataReader.DisposeAsync();
        }
        public IAsyncEnumerator<Object> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return this;
        }
        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(_dataReader.ReadAsync(_cancellationToken));
        }

        public Object Current => _dataReader.DbDataReader.GetValue(0);
    }
}
