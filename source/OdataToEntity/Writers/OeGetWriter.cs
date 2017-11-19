using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
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
            private readonly bool NavigationNextLink;
            private readonly ODataWriter Writer;

            public GetWriter(Uri baseUri, OeMetadataLevel metadataLevel, ODataWriter writer, bool navigationNextLink)
            {
                BaseUri = baseUri;
                MetadataLevel = metadataLevel;
                Writer = writer;
                NavigationNextLink = navigationNextLink;
            }

            private static Uri BuildNavigationMextPageLink(ODataResource entry, IEdmEntitySet entitySet, ExpandedNavigationSelectItem expandedNavigationSelectItem)
            {
                var keys = new List<KeyValuePair<String, Object>>(1);
                foreach (IEdmStructuralProperty key in entitySet.EntityType().Key())
                    foreach (ODataProperty property in entry.Properties)
                        if (property.Name == key.Name)
                        {
                            keys.Add(new KeyValuePair<String, Object>(property.Name, property.Value));
                            break;
                        }

                var segments = new List<ODataPathSegment>();
                segments.Add(new EntitySetSegment(entitySet));
                segments.Add(new KeySegment(keys, entitySet.EntityType(), entitySet));
                segments.AddRange(expandedNavigationSelectItem.PathToNavigationProperty);

                var odataUri = new ODataUri();
                odataUri.Path = new ODataPath(segments);
                odataUri.Filter = expandedNavigationSelectItem.FilterOption;
                odataUri.OrderBy = expandedNavigationSelectItem.OrderByOption;
                odataUri.SelectAndExpand = expandedNavigationSelectItem.SelectAndExpand;
                odataUri.Top = expandedNavigationSelectItem.TopOption;
                odataUri.Skip = expandedNavigationSelectItem.SkipOption;
                odataUri.QueryCount = expandedNavigationSelectItem.CountOption;

                return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
            }
            private static Uri BuildNextPageLink(OeParseUriContext parseUriContext, int count)
            {
                ODataUri odataUri = parseUriContext.ODataUri.Clone();
                odataUri.ServiceRoot = null;
                odataUri.QueryCount = null;
                odataUri.Top = parseUriContext.PageSize;
                odataUri.Skip = odataUri.Skip.GetValueOrDefault() + count;
                return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
            }
            private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
            {
                ODataResource entry = entryFactory.CreateEntry(entity);
                if (MetadataLevel == OeMetadataLevel.Full)
                    entry.Id = OeUriHelper.ComputeId(BaseUri, entryFactory.EntitySet, entry);
                return entry;
            }
            public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, Stream stream, OeParseUriContext parseUriContext)
            {
                var resourceSet = new ODataResourceSet();
                resourceSet.Count = asyncEnumerator.Count;
                Writer.WriteStart(resourceSet);

                int count = 0;
                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    Object value = asyncEnumerator.Current;
                    int? dummy;
                    ODataResource entry = CreateEntry(entryFactory, entryFactory.GetValue(value, out dummy));
                    Writer.WriteStart(entry);
                    foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                        WriteNavigationLink(value, navigationLink, entry, entryFactory.EntitySet);
                    Writer.WriteEnd();

                    count++;
                    if (count == parseUriContext.PageSize)
                    {
                        resourceSet.NextPageLink = BuildNextPageLink(parseUriContext, count);
                        break;
                    }
                }

                if (count < parseUriContext.PageSize && asyncEnumerator.Count.GetValueOrDefault() > count)
                    resourceSet.NextPageLink = BuildNextPageLink(parseUriContext, count);

                Writer.WriteEnd();
            }
            private void WriteNavigationLink(Object value, OeEntryFactory entryFactory, ODataResource parentEntry, IEdmEntitySet parentEntitySet)
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
                        if (entryFactory.ExpandedNavigationSelectItem == null)
                            foreach (Object entity in (IEnumerable)navigationValue)
                            {
                                ODataResource navigationEntry = CreateEntry(entryFactory, entity);
                                Writer.WriteStart(navigationEntry);
                                foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                                    WriteNavigationLink(entity, navigationLink, navigationEntry, navigationLink.EntitySet);
                                Writer.WriteEnd();
                            }
                        else
                            resourceSet.NextPageLink = BuildNavigationMextPageLink(parentEntry, parentEntitySet, entryFactory.ExpandedNavigationSelectItem);
                        Writer.WriteEnd();
                    }
                    else
                    {
                        ODataResource navigationEntry = CreateEntry(entryFactory, navigationValue);
                        Writer.WriteStart(navigationEntry);
                        foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                            WriteNavigationLink(navigationValue, navigationLink, navigationEntry, navigationLink.EntitySet);
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
                var getWriter = new GetWriter(baseUri, parseUriContext.Headers.MetadataLevel, writer, false);
                await getWriter.SerializeAsync(entryFactory, asyncEnumerator, stream, parseUriContext);
            }
        }
    }
}

