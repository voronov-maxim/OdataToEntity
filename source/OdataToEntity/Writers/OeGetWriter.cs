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
        private readonly struct GetWriter
        {
            private readonly OeQueryContext QueryContext;
            private readonly ODataWriter Writer;

            public GetWriter(OeQueryContext queryContex, ODataWriter writer)
            {
                QueryContext = queryContex;
                Writer = writer;
            }

            private static Uri BuildNavigationNextPageLink(ODataResource entry, ExpandedNavigationSelectItem expandedNavigationSelectItem)
            {
                var segment = (NavigationPropertySegment)expandedNavigationSelectItem.PathToNavigationProperty.LastSegment;
                ResourceRangeVariableReferenceNode refNode = OeGetParser.CreateRangeVariableReferenceNode((IEdmEntitySet)segment.NavigationSource);
                IEdmNavigationProperty navigationProperty = segment.NavigationProperty;

                var keys = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
                if (navigationProperty.IsPrincipal())
                {
                    IEnumerator<IEdmStructuralProperty> dependentProperties = navigationProperty.Partner.DependentProperties().GetEnumerator();
                    foreach (IEdmStructuralProperty key in navigationProperty.Partner.PrincipalProperties())
                        foreach (ODataProperty property in entry.Properties)
                            if (property.Name == key.Name)
                            {
                                dependentProperties.MoveNext();
                                keys.Add(new KeyValuePair<IEdmStructuralProperty, Object>(dependentProperties.Current, property.Value));
                                break;
                            }
                }
                else
                {
                    IEnumerator<IEdmStructuralProperty> principalProperties = navigationProperty.PrincipalProperties().GetEnumerator();
                    foreach (IEdmStructuralProperty key in navigationProperty.DependentProperties())
                        foreach (ODataProperty property in entry.Properties)
                            if (property.Name == key.Name)
                            {
                                principalProperties.MoveNext();
                                keys.Add(new KeyValuePair<IEdmStructuralProperty, Object>(principalProperties.Current, property.Value));
                                break;
                            }
                }

                BinaryOperatorNode filterExpression = OeGetParser.CreateFilterExpression(refNode, keys);
                if (expandedNavigationSelectItem.FilterOption != null)
                    filterExpression = new BinaryOperatorNode(BinaryOperatorKind.And, filterExpression, expandedNavigationSelectItem.FilterOption.Expression);

                var segments = new ODataPathSegment[] { new EntitySetSegment((IEdmEntitySet)refNode.NavigationSource) };

                var odataUri = new ODataUri()
                {
                    Path = new ODataPath(segments),
                    Filter = new FilterClause(filterExpression, refNode.RangeVariable),
                    OrderBy = expandedNavigationSelectItem.OrderByOption,
                    SelectAndExpand = expandedNavigationSelectItem.SelectAndExpand,
                    Top = expandedNavigationSelectItem.TopOption,
                    Skip = expandedNavigationSelectItem.SkipOption,
                    QueryCount = expandedNavigationSelectItem.CountOption
                };

                return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
            }
            private static Uri BuildNextPageLink(OeQueryContext queryContext, Object value)
            {
                ODataUri nextOdataUri = queryContext.ODataUri.Clone();
                nextOdataUri.ServiceRoot = null;
                nextOdataUri.QueryCount = null;
                nextOdataUri.Top = queryContext.PageSize;
                nextOdataUri.Skip = null;
                nextOdataUri.SkipToken = OeSkipTokenParser.GetSkipToken(queryContext.EdmModel, queryContext.SkipTokenAccessors, value);

                return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
            }
            private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
            {
                ODataResource entry = entryFactory.CreateEntry(entity);
                if (QueryContext.MetadataLevel == OeMetadataLevel.Full)
                    entry.Id = OeUriHelper.ComputeId(QueryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
                return entry;
            }
            private static bool IsNullEntry(ODataResource entry)
            {
                foreach (ODataProperty property in entry.Properties)
                    if (property.Value != null)
                        return false;

                return true;
            }
            public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
            {
                var resourceSet = new ODataResourceSet() { Count = asyncEnumerator.Count };
                Writer.WriteStart(resourceSet);

                Object value = null;
                int count = 0;
                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    value = asyncEnumerator.Current;
                    ODataResource entry = CreateEntry(entryFactory, entryFactory.GetValue(value, out _));
                    Writer.WriteStart(entry);

                    foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                        WriteNavigationLink(value, navigationLink, entry, entryFactory.EntitySet);

                    if (QueryContext.NavigationNextLink)
                        foreach (ExpandedNavigationSelectItem item in QueryContext.GetExpandedNavigationSelectItems())
                            WriteNavigationNextLink(entry, item);

                    Writer.WriteEnd();
                    count++;
                }

                if (queryContext.PageSize > 0 && count > 0 && (asyncEnumerator.Count ?? Int32.MaxValue) > count)
                    resourceSet.NextPageLink = BuildNextPageLink(queryContext, value);

                Writer.WriteEnd();
            }
            private void WriteNavigationLink(Object value, OeEntryFactory entryFactory, ODataResource parentEntry, IEdmEntitySet parentEntitySet)
            {
                Writer.WriteStart(entryFactory.ResourceInfo);

                Object navigationValue = entryFactory.GetValue(value, out int? count);
                if (navigationValue == null)
                {
                    Writer.WriteStart((ODataResource)null);
                    Writer.WriteEnd();
                }
                else
                {
                    if (entryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
                    {
                        Writer.WriteStart(new ODataResourceSet() { Count = count });
                        foreach (Object entity in (IEnumerable)navigationValue)
                        {
                            ODataResource navigationEntry = CreateEntry(entryFactory, entity);
                            Writer.WriteStart(navigationEntry);
                            foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                                WriteNavigationLink(entity, navigationLink, navigationEntry, navigationLink.EntitySet);
                            Writer.WriteEnd();
                        }
                        Writer.WriteEnd();
                    }
                    else
                    {
                        ODataResource navigationEntry = CreateEntry(entryFactory, navigationValue);
                        if (IsNullEntry(navigationEntry))
                        {
                            Writer.WriteStart((ODataResource)null);
                            Writer.WriteEnd();
                        }
                        else
                        {
                            Writer.WriteStart(navigationEntry);
                            foreach (OeEntryFactory navigationLink in entryFactory.NavigationLinks)
                                WriteNavigationLink(navigationValue, navigationLink, navigationEntry, navigationLink.EntitySet);
                            Writer.WriteEnd();
                        }
                    }
                }

                Writer.WriteEnd();
            }
            private void WriteNavigationNextLink(ODataResource parentEntry, ExpandedNavigationSelectItem item)
            {
                var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                var resourceInfo = new ODataNestedResourceInfo()
                {
                    IsCollection = true,
                    Name = segment.NavigationProperty.Name
                };

                Writer.WriteStart(resourceInfo);

                var resourceSet = new ODataResourceSet() { NextPageLink = BuildNavigationNextPageLink(parentEntry, item) };
                Writer.WriteStart(resourceSet);
                Writer.WriteEnd();

                Writer.WriteEnd();
            }
        }

        public static async Task SerializeAsync(OeQueryContext queryContext, Db.OeAsyncEnumerator asyncEnumerator, String contentType, Stream stream)
        {
            OeEntryFactory entryFactory = queryContext.EntryFactory;
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = queryContext.ODataUri.ServiceRoot,
                EnableMessageStreamDisposal = false,
                ODataUri = queryContext.ODataUri,
                Validations = ValidationKinds.ThrowIfTypeConflictsWithMetadata | ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            IODataResponseMessage responseMessage = new OeInMemoryMessage(stream, contentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, queryContext.EdmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);
                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(entryFactory.EntitySet, entryFactory.EntityType);
                var getWriter = new GetWriter(queryContext, writer);
                await getWriter.SerializeAsync(entryFactory, asyncEnumerator, queryContext).ConfigureAwait(false);
            }
        }
    }
}

