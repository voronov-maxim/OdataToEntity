using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OeBatchParser
    {
        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OeBatchParser(Uri baseUri, Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _baseUri = baseUri;
            _dataAdapter = dataAdapter;
            _model = model;
        }

        private void AddToEntitySet(Object dataContext, in OeOperationMessage operation)
        {
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.EntitySetAdapters.FindByEntitySet(operation.EntitySet);
            switch (operation.Method)
            {
                case ODataConstants.MethodDelete:
                    entitySetAdapter.RemoveEntity(dataContext, operation.Entry);
                    break;
                case ODataConstants.MethodPatch:
                    entitySetAdapter.AttachEntity(dataContext, operation.Entry);
                    break;
                case ODataConstants.MethodPost:
                    entitySetAdapter.AddEntity(dataContext, operation.Entry);
                    break;
                default:
                    throw new NotImplementedException(operation.Method);
            }
        }
        public async Task ExecuteAsync(Stream requestStream, Stream responseStream, String contentType, CancellationToken cancellationToken)
        {
            OeBatchMessage batchMessage = OeBatchMessage.CreateBatchMessage(_model, _baseUri, requestStream, contentType);
            if (batchMessage.Changeset == null)
                await ExecuteOperation(batchMessage.Operation, cancellationToken).ConfigureAwait(false);
            else
                await ExecuteChangeset(batchMessage.Changeset, cancellationToken).ConfigureAwait(false);

            var batchWriter = new Writers.OeBatchWriter(_model, _baseUri);
            batchWriter.Write(responseStream, batchMessage);
        }
        private async Task ExecuteChangeset(IReadOnlyList<OeOperationMessage> changeset, CancellationToken cancellationToken)
        {
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                foreach (OeOperationMessage operation in changeset)
                    AddToEntitySet(dataContext, operation);
                await _dataAdapter.SaveChangesAsync(_model, dataContext, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
        private async Task ExecuteOperation(OeOperationMessage operation, CancellationToken cancellationToken)
        {
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                AddToEntitySet(dataContext, operation);
                await _dataAdapter.SaveChangesAsync(_model, dataContext, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
    }
}
