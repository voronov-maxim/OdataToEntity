using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public sealed class OeBatchParser
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

        private void AddToEntitySet(Object dataContext, OeOperationMessage operation)
        {
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.GetEntitySetAdapter(operation.EntityItem.EntitySet.Name);
            switch (operation.Method)
            {
                case ODataConstants.MethodDelete:
                    entitySetAdapter.RemoveEntity(dataContext, operation.EntityItem.Entity);
                    break;
                case ODataConstants.MethodPatch:
                    entitySetAdapter.AttachEntity(dataContext, operation.EntityItem.Entity);
                    break;
                case ODataConstants.MethodPost:
                    entitySetAdapter.AddEntity(dataContext, operation.EntityItem.Entity);
                    break;
                default:
                    throw new NotImplementedException(operation.Method);
            }
        }
        public async Task ExecuteAsync(Stream requestStream, Stream responseStream, String contentType, CancellationToken cancellationToken)
        {
            var context = new OeMessageContext(_baseUri, _model, _dataAdapter.EntitySetMetaAdapters);
            OeBatchMessage batchMessage = OeBatchMessage.CreateBatchMessage(context, requestStream, contentType);
            if (batchMessage.Changeset != null)
                await ExecuteChangeset(batchMessage.Changeset, cancellationToken).ConfigureAwait(false);
            else if (batchMessage.Operation != null)
                await ExecuteOperation(batchMessage.Operation, cancellationToken).ConfigureAwait(false);

            var batchWriter = new Writers.OeBatchWriter(context.BaseUri, context.Model);
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
