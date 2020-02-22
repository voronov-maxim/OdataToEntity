using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public readonly struct OeBatchWriter
    {
        private readonly IEdmModel _model;
        private readonly ODataMessageWriterSettings _settings;

        public OeBatchWriter(IEdmModel model, Uri baseUri)
        {
            _model = model;

            _settings = new ODataMessageWriterSettings()
            {
                BaseUri = baseUri,
                Version = ODataVersion.V4,
                ODataUri = new ODataUri() { ServiceRoot = baseUri },
                EnableMessageStreamDisposal = false
            };
        }

        public async Task WriteAsync(Stream stream, OeBatchMessage batchMessage)
        {
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, batchMessage.ContentType);
            using (var messageWriter = new ODataMessageWriter(responseMessage, _settings))
            {
                ODataBatchWriter writer = await messageWriter.CreateODataBatchWriterAsync().ConfigureAwait(false);

                await writer.WriteStartBatchAsync().ConfigureAwait(false);
                await WriteBatchAsync(writer, batchMessage).ConfigureAwait(false);
                await writer.WriteEndBatchAsync().ConfigureAwait(false);
            }
        }
        private async Task WriteBatchAsync(ODataBatchWriter writer, OeBatchMessage batchMessage)
        {
            if (batchMessage.Changeset == null)
                await WriteOperationAsync(writer, batchMessage.Operation);
            else
                await WriteChangesetAsync(writer, batchMessage.Changeset);
        }
        private async Task WriteChangesetAsync(ODataBatchWriter writer, IReadOnlyList<OeOperationMessage> changeset)
        {
            await writer.WriteStartChangesetAsync();
            foreach (OeOperationMessage operation in changeset)
                await WriteOperationAsync(writer, operation);
            await writer.WriteEndChangesetAsync();
        }
        private async Task WriteEntityAsync(IEdmEntitySet entitySet, ODataResource entry, Stream stream)
        {
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, null);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, _settings, _model.GetEdmModel(entitySet)))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.Resource);
                ODataWriter writer = await messageWriter.CreateODataResourceWriterAsync(entitySet, entitySet.EntityType());

                await writer.WriteStartAsync(entry);
                await writer.WriteEndAsync();
            }
        }
        private async Task WriteOperationAsync(ODataBatchWriter writer, OeOperationMessage operation)
        {
            ODataBatchOperationResponseMessage operationMessage = await writer.CreateOperationResponseMessageAsync(operation.ContentId);
            operationMessage.SetHeader("Location", operation.RequestUrl.AbsoluteUri);
            operationMessage.SetHeader(ODataConstants.ContentTypeHeader, operation.ContentType);
            operationMessage.StatusCode = (int)operation.StatusCode;

            if (operation.StatusCode != HttpStatusCode.NoContent)
                using (Stream stream = await operationMessage.GetStreamAsync())
                    await WriteEntityAsync(operation.EntitySet, operation.Entry, stream);
        }
    }
}
