using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

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

        public void Write(Stream stream, in OeBatchMessage batchMessage)
        {
            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, batchMessage.ContentType);
            var settings = new ODataMessageWriterSettings()
            {
                Version = ODataVersion.V4,
                EnableMessageStreamDisposal = false,
                MessageQuotas = new ODataMessageQuotas()
            };
            var messageWriter = new ODataMessageWriter(responseMessage, settings);
            ODataBatchWriter writer = messageWriter.CreateODataBatchWriter();

            writer.WriteStartBatch();
            WriteBatch(writer, batchMessage);
            writer.WriteEndBatch();
        }
        private void WriteBatch(ODataBatchWriter writer, in OeBatchMessage batchMessage)
        {
            if (batchMessage.Changeset == null)
                WriteOperation(writer, batchMessage.Operation);
            else
                WriteChangeset(writer, batchMessage.Changeset);
        }
        private void WriteChangeset(ODataBatchWriter writer, IReadOnlyList<OeOperationMessage> changeset)
        {
            writer.WriteStartChangeset();
            foreach (OeOperationMessage operation in changeset)
                WriteOperation(writer, operation);
            writer.WriteEndChangeset();
        }
        private void WriteEntity(IEdmEntitySet entitySet, ODataResource entry, Stream stream)
        {
            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, null);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, _settings, _model.GetEdmModel(entitySet)))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.Resource);
                ODataWriter writer = messageWriter.CreateODataResourceWriter(entitySet, entitySet.EntityType());

                writer.WriteStart(entry);
                writer.WriteEnd();
            }
        }
        private void WriteOperation(ODataBatchWriter writer, in OeOperationMessage operation)
        {
            ODataBatchOperationResponseMessage operationMessage = writer.CreateOperationResponseMessage(operation.ContentId);
            operationMessage.SetHeader("Location", operation.RequestUrl.AbsoluteUri);
            operationMessage.SetHeader(ODataConstants.ContentTypeHeader, operation.ContentType);
            operationMessage.StatusCode = (int)operation.StatusCode;

            if (operation.StatusCode != HttpStatusCode.NoContent)
                using (Stream stream = operationMessage.GetStream())
                    WriteEntity(operation.EntitySet, operation.Entry, stream);
        }
    }
}
