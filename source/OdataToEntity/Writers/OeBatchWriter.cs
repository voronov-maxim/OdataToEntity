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

        public async ValueTask WriteBatchAsync(Stream stream, OeBatchMessage batchMessage)
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
        private async ValueTask WriteBatchAsync(ODataBatchWriter writer, OeBatchMessage batchMessage)
        {
            if (batchMessage.Changeset == null)
                await WriteBatchOperationAsync(writer, batchMessage.Operation).ConfigureAwait(false);
            else
                await WriteChangesetAsync(writer, batchMessage.Changeset).ConfigureAwait(false);
        }
        private async ValueTask WriteBatchOperationAsync(ODataBatchWriter writer, OeOperationMessage operation)
        {
            ODataBatchOperationResponseMessage operationMessage = await writer.CreateOperationResponseMessageAsync(operation.ContentId);
            operationMessage.SetHeader("Location", operation.RequestUrl.AbsoluteUri);
            operationMessage.SetHeader(ODataConstants.ContentTypeHeader, operation.ContentType);
            operationMessage.StatusCode = (int)operation.StatusCode;

            if (operation.StatusCode != HttpStatusCode.NoContent)
                using (Stream stream = await operationMessage.GetStreamAsync().ConfigureAwait(false))
                    await WriteEntityAsync(operation.EntitySet, operation.Entry, stream).ConfigureAwait(false);
        }
        private async ValueTask WriteChangesetAsync(ODataBatchWriter writer, IReadOnlyList<OeOperationMessage> changeset)
        {
            await writer.WriteStartChangesetAsync().ConfigureAwait(false);
            foreach (OeOperationMessage operation in changeset)
                await WriteBatchOperationAsync(writer, operation).ConfigureAwait(false);
            await writer.WriteEndChangesetAsync().ConfigureAwait(false);
        }
        private async ValueTask WriteEntityAsync(IEdmEntitySet entitySet, ODataResource entry, Stream stream)
        {
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, null);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, _settings, _model.GetEdmModel(entitySet)))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.Resource);
                ODataWriter writer = await messageWriter.CreateODataResourceWriterAsync(entitySet, entitySet.EntityType()).ConfigureAwait(false);

                await writer.WriteStartAsync(entry).ConfigureAwait(false);
                await writer.WriteEndAsync().ConfigureAwait(false);
            }
        }
        public async ValueTask WriteOperationAsync(Stream stream, OeOperationMessage operation)
        {
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, operation.ContentType);
            using (var messageWriter = new ODataMessageWriter(responseMessage, _settings))
                await WriteEntityAsync(operation.EntitySet, operation.Entry, stream).ConfigureAwait(false);
        }
    }
}
