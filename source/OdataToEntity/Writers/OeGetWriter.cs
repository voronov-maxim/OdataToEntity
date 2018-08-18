using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public static class OeGetWriter
    {
        private readonly struct GetWriter
        {
            private readonly OeQueryContext _queryContext;
            private readonly ODataWriter _writer;

            public GetWriter(OeQueryContext queryContex, ODataWriter writer)
            {
                _queryContext = queryContex;
                _writer = writer;
            }

            private static Uri BuildNavigationNextPageLink(OeEntryFactory entryFactory, ExpandedNavigationSelectItem expandedNavigationSelectItem, Object value)
            {
                var segment = (NavigationPropertySegment)expandedNavigationSelectItem.PathToNavigationProperty.LastSegment;
                IEdmNavigationProperty navigationProperty = segment.NavigationProperty;
                if (navigationProperty.IsPrincipal())
                    navigationProperty = navigationProperty.Partner;

                var keys = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
                IEnumerator<IEdmStructuralProperty> dependentProperties = navigationProperty.DependentProperties().GetEnumerator();
                foreach (IEdmStructuralProperty key in navigationProperty.PrincipalProperties())
                {
                    dependentProperties.MoveNext();
                    Object keyValue = entryFactory.GetAccessorByName(key.Name).GetValue(value);
                    keys.Add(new KeyValuePair<IEdmStructuralProperty, Object>(dependentProperties.Current, keyValue));
                }

                ResourceRangeVariableReferenceNode refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode((IEdmEntitySet)segment.NavigationSource);
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
                if (_queryContext.MetadataLevel == OeMetadataLevel.Full)
                    entry.Id = OeUriHelper.ComputeId(_queryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
                return entry;
            }
            public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
            {
                var resourceSet = new ODataResourceSet() { Count = asyncEnumerator.Count };
                _writer.WriteStart(resourceSet);

                Object buffer = null;
                int count = 0;
                var dbEnumerator = new Db.OeDbEnumerator(asyncEnumerator, entryFactory);
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    Object value = dbEnumerator.Current;
                    await WriteEntry(dbEnumerator, value, _queryContext.NavigationNextLink).ConfigureAwait(false);
                    count++;
                    buffer = dbEnumerator.ClearBuffer();
                }

                if (queryContext.PageSize > 0 && count > 0 && (asyncEnumerator.Count ?? Int32.MaxValue) > count)
                    resourceSet.NextPageLink = BuildNextPageLink(queryContext, buffer);

                _writer.WriteEnd();
            }
            private async Task WriteEagerNestedCollection(Db.OeDbEnumerator dbEnumerator)
            {
                var items = new List<Object>();
                do
                {
                    Object value = dbEnumerator.Current;
                    if (value != null)
                        items.Add(value);
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

                _writer.WriteStart(new ODataResourceSet() { Count = items.Count });
                for (int i = 0; i < items.Count; i++)
                    await WriteEntry(dbEnumerator, items[i], false).ConfigureAwait(false);
                _writer.WriteEnd();
            }
            private async Task WriteEntry(Db.OeDbEnumerator dbEnumerator, Object value, bool navigationNextLink)
            {
                OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
                ODataResource entry = CreateEntry(entryFactory, value);
                _writer.WriteStart(entry);
                for (int i = 0; i < entryFactory.NavigationLinks.Count; i++)
                    await WriteNavigationLink(dbEnumerator.CreateChild(entryFactory.NavigationLinks[i])).ConfigureAwait(false);

                if (navigationNextLink)
                    foreach (ExpandedNavigationSelectItem item in _queryContext.GetExpandedNavigationSelectItems())
                        WriteNavigationNextLink(entryFactory, item, value);

                _writer.WriteEnd();
            }
            private async Task WriteLazyNestedCollection(Db.OeDbEnumerator dbEnumerator)
            {
                _writer.WriteStart(new ODataResourceSet());
                do
                {
                    Object value = dbEnumerator.Current;
                    if (value != null)
                        await WriteEntry(dbEnumerator, value, false).ConfigureAwait(false);
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));
                _writer.WriteEnd();
            }
            private async Task WriteNavigationLink(Db.OeDbEnumerator dbEnumerator)
            {
                _writer.WriteStart(dbEnumerator.EntryFactory.ResourceInfo);
                if (dbEnumerator.EntryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
                {
                    if (dbEnumerator.EntryFactory.CountOption.GetValueOrDefault())
                        await WriteEagerNestedCollection(dbEnumerator).ConfigureAwait(false);
                    else
                        await WriteLazyNestedCollection(dbEnumerator).ConfigureAwait(false);
                }
                else
                    await WriteNestedItem(dbEnumerator);
                _writer.WriteEnd();
            }
            private void WriteNavigationNextLink(OeEntryFactory entryFactory, ExpandedNavigationSelectItem item, Object value)
            {
                var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                var resourceInfo = new ODataNestedResourceInfo()
                {
                    IsCollection = true,
                    Name = segment.NavigationProperty.Name
                };

                _writer.WriteStart(resourceInfo);

                var resourceSet = new ODataResourceSet() { NextPageLink = BuildNavigationNextPageLink(entryFactory, item, value) };
                _writer.WriteStart(resourceSet);
                _writer.WriteEnd();

                _writer.WriteEnd();
            }
            private async Task WriteNestedItem(Db.OeDbEnumerator dbEnumerator)
            {
                Object value = dbEnumerator.Current;
                if (value == null)
                {
                    _writer.WriteStart((ODataResource)null);
                    _writer.WriteEnd();
                }
                else
                    await WriteEntry(dbEnumerator, value, false).ConfigureAwait(false);
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

