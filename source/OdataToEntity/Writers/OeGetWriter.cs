using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser.Aggregation;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public sealed class OeGetWriter
    {
        private readonly Uri _baseUri;
        private readonly IEdmModel _model;
        private readonly ODataMessageWriterSettings _settings;

        public OeGetWriter(Uri baseUri, IEdmModel model)
        {
            _baseUri = baseUri;
            _model = model;

            _settings = new ODataMessageWriterSettings()
            {
                BaseUri = _baseUri,
                EnableMessageStreamDisposal = false,
                Validations = ValidationKinds.ThrowIfTypeConflictsWithMetadata | ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };
        }

        private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity, OeMetadataLevel metadataLevel)
        {
            ODataResource entry = entryFactory.CreateEntry(entity);
            if (metadataLevel == OeMetadataLevel.Full)
                entry.Id = OeUriHelper.ComputeId(_baseUri, entryFactory.EntitySet, entry);
            return entry;
        }
        public async Task SerializeAsync(ODataUri odataUri, OeEntryFactory entryFactory, Db.OeEntityAsyncEnumerator asyncEnumerator, OeRequestHeaders headers, Stream stream)
        {
            _settings.ODataUri = odataUri;

            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, headers.ContentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, _settings, _model))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);
                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(entryFactory.EntitySet, entryFactory.EntityType);
                writer.WriteStart(new ODataResourceSet());
                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    Object value = asyncEnumerator.Current;
                    ODataResource entry = CreateEntry(entryFactory, entryFactory.GetValue(value), headers.MetadataLevel);
                    writer.WriteStart(entry);
                    foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                        WriteNavigationLink(writer, value, navigationLink, headers.MetadataLevel);
                    writer.WriteEnd();
                }
                writer.WriteEnd();
            }
        }
        private void WriteNavigationLink(ODataWriter writer, Object value, OeEntryFactory navigationLink, OeMetadataLevel metadataLevel)
        {
            writer.WriteStart(navigationLink.Link);

            Object navigationValue = navigationLink.GetValue(value);
            if (navigationValue != null)
            {
                if (navigationLink.Link.IsCollection.GetValueOrDefault())
                {
                    writer.WriteStart(new ODataResourceSet());
                    foreach (Object entity in (IEnumerable)navigationValue)
                    {
                        ODataResource entry = CreateEntry(navigationLink, entity, metadataLevel);
                        writer.WriteStart(entry);
                        writer.WriteEnd();
                    }
                    writer.WriteEnd();
                }
                else
                {
                    ODataResource entry = CreateEntry(navigationLink, navigationValue, metadataLevel);
                    writer.WriteStart(entry);
                    writer.WriteEnd();
                }
            }

            writer.WriteEnd();
        }
    }
}
