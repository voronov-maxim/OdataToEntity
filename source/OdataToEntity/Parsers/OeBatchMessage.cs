using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OdataToEntity.Parsers
{
    public sealed class OeBatchMessage
    {
        private readonly IReadOnlyList<OeOperationMessage> _changeset;
        private readonly String _contentType;
        private readonly OeOperationMessage _operation;

        private OeBatchMessage(String contentType, IReadOnlyList<OeOperationMessage> changeset)
        {
            _contentType = contentType;
            _changeset = changeset;
        }
        private OeBatchMessage(String contentType, OeOperationMessage operation)
        {
            _contentType = contentType;
            _operation = operation;
        }

        public static OeBatchMessage CreateBatchMessage(OeMessageContext context, Stream requestStream, String contentType)
        {
            IODataRequestMessage requestMessage = new OeInMemoryMessage(requestStream, contentType);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings))
            {
                var batchMessage = new List<OeBatchMessage>();
                ODataBatchReader batchReader = messageReader.CreateODataBatchReader();
                while (batchReader.Read())
                {
                    if (batchReader.State == ODataBatchReaderState.ChangesetStart)
                    {
                        var operations = new List<OeOperationMessage>();
                        while (batchReader.Read() && batchReader.State != ODataBatchReaderState.ChangesetEnd)
                        {
                            if (batchReader.State == ODataBatchReaderState.Operation)
                            {
                                OeOperationMessage operation = OeOperationMessage.Create(context, batchReader);
                                operations.Add(operation);
                            }
                        }
                        return new OeBatchMessage(contentType, operations);
                    }
                    else if (batchReader.State == ODataBatchReaderState.Operation)
                    {
                        OeOperationMessage operation = OeOperationMessage.Create(context, batchReader);
                        return new OeBatchMessage(contentType, operation);
                    }
                }
            }

            throw new InvalidOperationException("batch not found");
        }

        public IReadOnlyList<OeOperationMessage> Changeset => _changeset;
        public String ContentType => _contentType;
        public OeOperationMessage Operation => _operation;
    }
}
