using Microsoft.OData;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public static class OeGetWriter
    {
        public static Task SerializeAsync(OeQueryContext queryContext, IAsyncEnumerator<Object> asyncEnumerator,
            String contentType, Stream stream, CancellationToken cancellationToken)
        {
            return SerializeAsync(queryContext, asyncEnumerator, contentType, stream, queryContext.EntryFactory, cancellationToken);
        }
        public static async Task SerializeAsync(OeQueryContext queryContext, IAsyncEnumerator<Object> asyncEnumerator,
            String contentType, Stream stream, OeEntryFactory entryFactory, CancellationToken cancellationToken)
        {
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = queryContext.ODataUri.ServiceRoot,
                EnableMessageStreamDisposal = false,
                ODataUri = queryContext.ODataUri,
                Validations = ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, contentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, queryContext.EdmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);
                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(entryFactory.EntitySet, entryFactory.EdmEntityType);
                var odataWriter = new OeODataWriter(queryContext, writer, cancellationToken);
                await odataWriter.WriteAsync(entryFactory, asyncEnumerator).ConfigureAwait(false);
            }
        }
    }
}

