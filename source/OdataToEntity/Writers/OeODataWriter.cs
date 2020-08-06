using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public readonly struct OeODataWriter
    {
        private readonly CancellationToken _cancellationToken;
        private readonly OeQueryContext _queryContext;
        private readonly ODataWriter _writer;

        public OeODataWriter(OeQueryContext queryContex, ODataWriter writer, CancellationToken cancellationToken)
        {
            _queryContext = queryContex;
            _writer = writer;
            _cancellationToken = cancellationToken;
        }

        private ODataResource CreateEntry(OeEntryFactory entryFactory, Object? entity)
        {
            ODataResource entry = entryFactory.CreateEntry(entity);
            if (_queryContext.MetadataLevel == OeMetadataLevel.Full)
                entry.Id = OeUriHelper.ComputeId(_queryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
            return entry;
        }
        public async Task WriteAsync(OeEntryFactory entryFactory, IAsyncEnumerator<Object?> asyncEnumerator)
        {
            var resourceSet = new ODataResourceSet() { Count = _queryContext.TotalCountOfItems };
            await _writer.WriteStartAsync(resourceSet).ConfigureAwait(false);

            Object? rawValue = null;
            int readCount = 0;
            Db.IOeDbEnumerator dbEnumerator = entryFactory.IsTuple ?
                (Db.IOeDbEnumerator)new Db.OeDbEnumerator(asyncEnumerator, entryFactory) : new Db.OeEntityDbEnumerator(asyncEnumerator, entryFactory);
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                await WriteEntry(dbEnumerator, dbEnumerator.Current).ConfigureAwait(false);
                readCount++;
                rawValue = dbEnumerator.RawValue;
                dbEnumerator.ClearBuffer();
            }

            if (rawValue != null)
            {
                var nextPageLinkBuilder = new OeNextPageLinkBuilder(_queryContext);
                resourceSet.NextPageLink = nextPageLinkBuilder.GetNextPageLinkRoot(entryFactory, readCount, _queryContext.TotalCountOfItems, rawValue);
            }

            await _writer.WriteEndAsync().ConfigureAwait(false);
        }
        private async Task WriteEntry(Db.IOeDbEnumerator dbEnumerator, Object? value)
        {
            OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
            ODataResource entry = CreateEntry(entryFactory, value);
            await _writer.WriteStartAsync(entry).ConfigureAwait(false);

            for (int i = 0; i < entryFactory.NavigationLinks.Count; i++)
                if (entryFactory.NavigationLinks[i].NextLink)
                    await WriteNavigationNextLink(entryFactory, entryFactory.NavigationLinks[i].NavigationSelectItem, value).ConfigureAwait(false);
                else
                    await WriteNavigation(dbEnumerator.CreateChild(entryFactory.NavigationLinks[i], _cancellationToken)).ConfigureAwait(false);
            await _writer.WriteEndAsync().ConfigureAwait(false);
        }
        private async Task WriteNavigation(Db.IOeDbEnumerator dbEnumerator)
        {
            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            bool isCollection = entryFactory.EdmNavigationProperty.Type.Definition is EdmCollectionType;
            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = isCollection,
                Name = entryFactory.EdmNavigationProperty.Name
            };
            await _writer.WriteStartAsync(resourceInfo).ConfigureAwait(false);

            if (isCollection)
                await WriteNavigationCollection(dbEnumerator).ConfigureAwait(false);
            else
                await WriteNavigationSingle(dbEnumerator).ConfigureAwait(false);

            await _writer.WriteEndAsync().ConfigureAwait(false);
        }
        private async Task WriteNavigationCollection(Db.IOeDbEnumerator dbEnumerator)
        {
            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            Object? value;
            int readCount = 0;
            ODataResourceSet? resourceSet = null;
            do
            {
                value = dbEnumerator.Current;
                if (value != null)
                {
                    if (resourceSet == null)
                    {
                        resourceSet = new ODataResourceSet();
                        if (entryFactory.NavigationSelectItem.CountOption.GetValueOrDefault())
                            resourceSet.Count = OeNextPageLinkBuilder.GetNestedCount(_queryContext.EdmModel, dbEnumerator);
                        await _writer.WriteStartAsync(resourceSet).ConfigureAwait(false);
                    }

                    await WriteEntry(dbEnumerator, value).ConfigureAwait(false);
                    readCount++;
                }
            }
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

            if (resourceSet == null)
            {
                resourceSet = new ODataResourceSet();
                if (entryFactory.NavigationSelectItem.CountOption.GetValueOrDefault())
                    resourceSet.Count = 0;
                await _writer.WriteStartAsync(resourceSet).ConfigureAwait(false);
            }
            else
            {
                var nextPageLinkBuilder = new OeNextPageLinkBuilder(_queryContext);
                resourceSet.NextPageLink = nextPageLinkBuilder.GetNextPageLinkNavigation(dbEnumerator, readCount, resourceSet.Count, value!);
            }

            await _writer.WriteEndAsync().ConfigureAwait(false);
        }
        private async Task WriteNavigationNextLink(OeEntryFactory parentEntryFactory, ExpandedNavigationSelectItem item, Object? value)
        {
            Uri? nextPageLink = new OeNextPageLinkBuilder(_queryContext).GetNavigationUri(parentEntryFactory, item, value);
            if (nextPageLink == null)
                return;

            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            bool isCollection = segment.NavigationProperty.Type.IsCollection();
            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = isCollection,
                Name = segment.NavigationProperty.Name
            };
            await _writer.WriteStartAsync(resourceInfo).ConfigureAwait(false);

            if (isCollection)
            {
                var resourceSet = new ODataResourceSet() { NextPageLink = nextPageLink };
                await _writer.WriteStartAsync(resourceSet).ConfigureAwait(false);
                await _writer.WriteEndAsync().ConfigureAwait(false);
            }
            else
                resourceInfo.Url = nextPageLink;

            await _writer.WriteEndAsync().ConfigureAwait(false);
        }
        private async Task WriteNavigationSingle(Db.IOeDbEnumerator dbEnumerator)
        {
            Object? value = dbEnumerator.Current;
            if (value == null)
            {
                await _writer.WriteStartAsync((ODataResource?)null).ConfigureAwait(false);
                await _writer.WriteEndAsync().ConfigureAwait(false);
            }
            else
                await WriteEntry(dbEnumerator, value).ConfigureAwait(false);
        }
    }
}