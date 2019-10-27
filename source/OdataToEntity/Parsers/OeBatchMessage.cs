#nullable enable
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OeBatchMessage
    {
        private OeBatchMessage(String contentType, IReadOnlyList<OeOperationMessage> changeset)
        {
            ContentType = contentType;
            Changeset = changeset;
            Operation = default;
        }
        private OeBatchMessage(String contentType, in OeOperationMessage operation)
        {
            ContentType = contentType;
            Changeset = null;
            Operation = operation;
        }

        public static async ValueTask<OeBatchMessage> CreateBatchMessage(IEdmModel edmModel, Uri baseUri, Stream requestStream, String contentType, IServiceProvider? serviceProvider = null)
        {
            IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(requestStream, contentType, serviceProvider);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings))
            {
                var batchMessage = new List<OeBatchMessage>();
                ODataBatchReader batchReader = await messageReader.CreateODataBatchReaderAsync();
                while (await batchReader.ReadAsync())
                {
                    if (batchReader.State == ODataBatchReaderState.ChangesetStart)
                    {
                        var operations = new List<OeOperationMessage>();
                        while (await batchReader.ReadAsync() && batchReader.State != ODataBatchReaderState.ChangesetEnd)
                            if (batchReader.State == ODataBatchReaderState.Operation)
                                operations.Add(await OeOperationMessage.Create(edmModel, baseUri, batchReader, serviceProvider));
                        return new OeBatchMessage(contentType, operations);
                    }
                    else if (batchReader.State == ODataBatchReaderState.Operation)
                        return new OeBatchMessage(contentType, await OeOperationMessage.Create(edmModel, baseUri, batchReader, serviceProvider));
                }
            }

            throw new InvalidOperationException("batch not found");
        }

        public IReadOnlyList<OeOperationMessage>? Changeset { get; }
        public String ContentType { get; }
        public OeOperationMessage Operation { get; }
    }
}
