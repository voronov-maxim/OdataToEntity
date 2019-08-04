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
        private sealed class ServiceProvider : IServiceProvider
        {
            private readonly IServiceProvider _parentServiceProvider;
            private readonly ODataMessageWriterSettings _writerSettings;

            public ServiceProvider(IServiceProvider parentServiceProvider, ODataMessageWriterSettings writerSettings)
            {
                _parentServiceProvider = parentServiceProvider;
                _writerSettings = writerSettings;
            }

            public Object GetService(Type serviceType)
            {
                if (serviceType == typeof(ODataMessageWriterSettings))
                    return _writerSettings;

                return _parentServiceProvider.GetService(serviceType);
            }
        }

        public static Task SerializeAsync(OeQueryContext queryContext, IAsyncEnumerator<Object> asyncEnumerator,
            String contentType, Stream stream, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            return SerializeAsync(queryContext, asyncEnumerator, contentType, stream, queryContext.EntryFactory, serviceProvider, cancellationToken);
        }
        public static async Task SerializeAsync(OeQueryContext queryContext, IAsyncEnumerator<Object> asyncEnumerator,
            String contentType, Stream stream, OeEntryFactory entryFactory, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = queryContext.ODataUri.ServiceRoot,
                EnableMessageStreamDisposal = false,
                ODataUri = queryContext.ODataUri,
                Validations = ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            if (serviceProvider != null)
                serviceProvider = new ServiceProvider(serviceProvider, settings);

            var syncToAsyncStream = new Infrastructure.SyncToAsyncStream(stream);//fix invoke Flush throw exception in kestrel 3.0
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(syncToAsyncStream, contentType, serviceProvider);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, queryContext.EdmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);
                ODataWriter writer = await messageWriter.CreateODataResourceSetWriterAsync(entryFactory.EntitySet, entryFactory.EdmEntityType);
                var odataWriter = new OeODataWriter(queryContext, writer, cancellationToken);
                await odataWriter.WriteAsync(entryFactory, asyncEnumerator).ConfigureAwait(false);
            }
        }
    }
}

