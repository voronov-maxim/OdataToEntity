using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
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
        private readonly IEdmModel _edmModel;
        private readonly IServiceProvider? _serviceProvider;

        public OeBatchParser(Uri baseUri, IEdmModel edmModel, IServiceProvider? serviceProvider = null)
        {
            _baseUri = baseUri;
            _edmModel = edmModel;
            _serviceProvider = serviceProvider;
        }

        private void AddToEntitySet(Object dataContext, in OeOperationMessage operation)
        {
            Db.OeEntitySetAdapter entitySetAdapter = _edmModel.GetEntitySetAdapter(operation.EntitySet);
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
            OeBatchMessage batchMessage = await OeBatchMessage.CreateBatchMessageAsync(_edmModel, _baseUri, requestStream, contentType, _serviceProvider);
            if (batchMessage.Changeset == null)
                await ExecuteOperationAsync(batchMessage.Operation, cancellationToken).ConfigureAwait(false);
            else
                await ExecuteChangesetAsync(batchMessage.Changeset, cancellationToken).ConfigureAwait(false);

            var batchWriter = new Writers.OeBatchWriter(_edmModel, _baseUri);
            await batchWriter.WriteBatchAsync(responseStream, batchMessage);
        }
        private async Task ExecuteChangesetAsync(IReadOnlyList<OeOperationMessage> changeset, CancellationToken cancellationToken)
        {
            Db.OeDataAdapter? dataAdapter = null;
            Object? dataContext = null;
            try
            {
                for (int i = 0; i < changeset.Count; i++)
                {
                    if (dataAdapter == null)
                        dataAdapter = _edmModel.GetDataAdapter(changeset[i].EntitySet.Container);
                    if (dataContext == null)
                        dataContext = dataAdapter.CreateDataContext();
                    AddToEntitySet(dataContext, changeset[i]);
                }

                if (dataAdapter != null && dataContext != null)
                {
                    await dataAdapter.SaveChangesAsync(dataContext, cancellationToken).ConfigureAwait(false);
                    for (int i = 0; i < changeset.Count; i++)
                    {
                        Db.OeEntitySetAdapter entitySetAdapter = dataAdapter.EntitySetAdapters.Find(changeset[i].EntitySet);
                        entitySetAdapter.UpdateEntityAfterSave(dataContext, changeset[i].Entry);
                    }
                }
            }
            finally
            {
                if (dataAdapter != null && dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
            }
        }
        private async Task ExecuteOperationAsync(OeOperationMessage operation, CancellationToken cancellationToken)
        {
            Db.OeDataAdapter dataAdapter = _edmModel.GetDataAdapter(operation.EntitySet.Container);
            Object? dataContext = null;
            try
            {
                dataContext = dataAdapter.CreateDataContext();
                AddToEntitySet(dataContext, operation);
                await dataAdapter.SaveChangesAsync(dataContext, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
            }
        }
        public async Task ExecuteOperationAsync(Uri requestUri, Stream requestStream, Stream responseStream, String contentType, String httpMethod, CancellationToken cancellationToken)
        {
            OeOperationMessage operationMessage = await OeBatchMessage.CreateOperationMessageAsync(
                _edmModel, _baseUri, requestUri, requestStream, contentType, httpMethod, _serviceProvider).ConfigureAwait(false);
            await ExecuteChangesetAsync(new[] { operationMessage }, cancellationToken).ConfigureAwait(false);

            var batchWriter = new Writers.OeBatchWriter(_edmModel, _baseUri);
            await batchWriter.WriteOperationAsync(responseStream, operationMessage).ConfigureAwait(false);
        }
    }
}
