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

        public static async ValueTask<OeBatchMessage> CreateBatchMessageAsync(IEdmModel edmModel, Uri baseUri, Stream requestStream, String contentType, IServiceProvider? serviceProvider = null)
        {
            IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(requestStream, contentType, serviceProvider);
            var settings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
            using (var messageReader = new ODataMessageReader(requestMessage, settings, edmModel))
            {
                var batchMessage = new List<OeBatchMessage>();
                ODataBatchReader batchReader = await messageReader.CreateODataBatchReaderAsync().ConfigureAwait(false);
                while (await batchReader.ReadAsync().ConfigureAwait(false))
                {
                    if (batchReader.State == ODataBatchReaderState.ChangesetStart)
                    {
                        var operations = new List<OeOperationMessage>();
                        while (await batchReader.ReadAsync().ConfigureAwait(false) && batchReader.State != ODataBatchReaderState.ChangesetEnd)
                            if (batchReader.State == ODataBatchReaderState.Operation)
                                operations.Add(await OeOperationMessage.CreateAsync(edmModel, baseUri, batchReader, serviceProvider).ConfigureAwait(false));
                        return new OeBatchMessage(contentType, operations);
                    }
                    else if (batchReader.State == ODataBatchReaderState.Operation)
                        return new OeBatchMessage(contentType, await OeOperationMessage.CreateAsync(edmModel, baseUri, batchReader, serviceProvider).ConfigureAwait(false));
                }
            }

            throw new InvalidOperationException("batch not found");
        }
        public static ValueTask<OeOperationMessage> CreateOperationMessageAsync(IEdmModel edmModel, Uri baseUri, Uri requestUri, Stream requestStream, String contentType, String httpMethod, IServiceProvider? serviceProvider = null)
        {
            IODataRequestMessage requestMessage = new Infrastructure.OeInMemoryMessage(requestStream, contentType, requestUri, httpMethod, serviceProvider);
            return OeOperationMessage.CreateAsync(edmModel, baseUri, requestMessage, serviceProvider);
        }

        public IReadOnlyList<OeOperationMessage>? Changeset { get; }
        public String ContentType { get; }
        public OeOperationMessage Operation { get; }
    }
}
