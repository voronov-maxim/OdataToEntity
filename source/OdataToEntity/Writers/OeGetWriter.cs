using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public static class OeGetWriter
    {
        private struct GetWriter
        {
            private readonly Uri BaseUri;
            private readonly OeMetadataLevel MetadataLevel;
            private readonly ODataWriter Writer;

            public GetWriter(Uri baseUri, OeMetadataLevel metadataLevel, ODataWriter writer)
            {
                BaseUri = baseUri;
                MetadataLevel = metadataLevel;
                Writer = writer;
            }

            private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
            {
                ODataResource entry = entryFactory.CreateEntry(entity);
                if (MetadataLevel == OeMetadataLevel.Full)
                    entry.Id = OeUriHelper.ComputeId(BaseUri, entryFactory.EntitySet, entry);
                return entry;
            }
            public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, Stream stream)
            {
                var resourceSet = new ODataResourceSet();
                resourceSet.Count = asyncEnumerator.Count;
                Writer.WriteStart(resourceSet);

                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    Object value = asyncEnumerator.Current;
                    int? dummy;
                    ODataResource entry = CreateEntry(entryFactory, entryFactory.GetValue(value, out dummy));
                    Writer.WriteStart(entry);
                    foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                        WriteNavigationLink(value, navigationLink);
                    Writer.WriteEnd();
                }

                Writer.WriteEnd();
            }
            private void WriteNavigationLink(Object value, OeEntryFactory entryFactory)
            {
                Writer.WriteStart(entryFactory.ResourceInfo);

                int? count;
                Object navigationValue = entryFactory.GetValue(value, out count);
                if (navigationValue == null)
                {
                    Writer.WriteStart((ODataResource)null);
                    Writer.WriteEnd();
                }
                else
                {
                    if (entryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
                    {
                        var resourceSet = new ODataResourceSet();
                        resourceSet.Count = count;

                        Writer.WriteStart(resourceSet);
                        foreach (Object entity in (IEnumerable)navigationValue)
                        {
                            ODataResource entry = CreateEntry(entryFactory, entity);
                            Writer.WriteStart(entry);
                            foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                                WriteNavigationLink(entity, navigationLink);
                            Writer.WriteEnd();
                        }
                        Writer.WriteEnd();
                    }
                    else
                    {
                        ODataResource entry = CreateEntry(entryFactory, navigationValue);
                        Writer.WriteStart(entry);
                        foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                            WriteNavigationLink(navigationValue, navigationLink);
                        Writer.WriteEnd();
                    }
                }

                Writer.WriteEnd();
            }
        }

        public static async Task SerializeAsync(Uri baseUri, OeParseUriContext parseUriContext, Db.OeAsyncEnumerator asyncEnumerator, Stream stream)
        {
            IEdmModel edmModel = parseUriContext.EdmModel;
            OeEntryFactory entryFactory = parseUriContext.EntryFactory;
            String contentType = parseUriContext.Headers.ContentType;

            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = baseUri,
                EnableMessageStreamDisposal = false,
                ODataUri = parseUriContext.ODataUri,
                Validations = ValidationKinds.ThrowIfTypeConflictsWithMetadata | ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, contentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, edmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);
                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(entryFactory.EntitySet, entryFactory.EntityType);
                var getWriter = new GetWriter(baseUri, parseUriContext.Headers.MetadataLevel, writer);
                await getWriter.SerializeAsync(entryFactory, asyncEnumerator, stream);
            }
        }
    }
}

