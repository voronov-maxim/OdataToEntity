using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public sealed class OeEfCoreDataReaderAsyncEnumerator : Db.OeAsyncEnumerator
    {
        private readonly RelationalDataReader _dataReader;

        public OeEfCoreDataReaderAsyncEnumerator(RelationalDataReader dataReader, CancellationToken cancellationToken) : base(cancellationToken)
        {
            _dataReader = dataReader;
        }

        public override void Dispose() => _dataReader.Dispose();
        public override Task<bool> MoveNextAsync() => _dataReader.ReadAsync();

        public override Object Current => _dataReader.DbDataReader.GetValue(0);
    }
}
