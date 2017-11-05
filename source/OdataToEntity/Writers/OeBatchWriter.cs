using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace OdataToEntity.Writers
{
    internal sealed class OeBatchWriter
    {
        private readonly IEdmModel _model;
        private readonly ODataMessageWriterSettings _settings;

        public OeBatchWriter(Uri baseUri, IEdmModel model)
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

        public void Write(Stream stream, OeBatchMessage batchMessage)
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
        private void WriteBatch(ODataBatchWriter writer, OeBatchMessage batchMessage)
        {
            if (batchMessage.Changeset != null)
                WriteChangeset(writer, batchMessage.Changeset);
            if (batchMessage.Operation != null)
                WriteOperation(writer, batchMessage.Operation);
        }
        private void WriteChangeset(ODataBatchWriter writer, IReadOnlyList<OeOperationMessage> changeset)
        {
            writer.WriteStartChangeset();
            foreach (OeOperationMessage operation in changeset)
                WriteOperation(writer, operation);
            writer.WriteEndChangeset();
        }
        private void WriteEntity(Stream stream, OeEntityItem entityItem)
        {
            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, null);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, _settings, _model))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.Resource);
                ODataWriter writer = messageWriter.CreateODataResourceWriter(entityItem.EntitySet, entityItem.EntityType);

                writer.WriteStart(entityItem.Entry);
                writer.WriteEnd();
            }
        }
        private void WriteOperation(ODataBatchWriter writer, OeOperationMessage operation)
        {
            ODataBatchOperationResponseMessage operationMessage = writer.CreateOperationResponseMessage(operation.ContentId);
            operationMessage.SetHeader("Location", operation.RequestUrl.AbsoluteUri);
            operationMessage.SetHeader(ODataConstants.ContentTypeHeader, operation.ContentType);
            operationMessage.StatusCode = (int)operation.StatusCode;

            if (operation.StatusCode != HttpStatusCode.NoContent)
                using (Stream stream = operationMessage.GetStream())
                    WriteEntity(stream, operation.EntityItem);
        }
    }
}
