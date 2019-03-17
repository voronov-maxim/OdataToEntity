using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.Writers
{
    public readonly struct OeODataWriter
    {
        private readonly OeQueryContext _queryContext;
        private readonly ODataWriter _writer;

        public OeODataWriter(OeQueryContext queryContex, ODataWriter writer)
        {
            _queryContext = queryContex;
            _writer = writer;
        }

        private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
        {
            ODataResource entry = entryFactory.CreateEntry(entity);
            if (_queryContext.MetadataLevel == OeMetadataLevel.Full)
                entry.Id = OeUriHelper.ComputeId(_queryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
            return entry;
        }
        private static IEnumerable<ExpandedNavigationSelectItem> GetExpandedNavigationSelectItems(SelectExpandClause selectAndExpand)
        {
            foreach (SelectItem selectItem in selectAndExpand.SelectedItems)
                if (selectItem is ExpandedNavigationSelectItem item)
                {
                    var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationEdmProperty = segment.NavigationProperty;
                    if (navigationEdmProperty.Type.Definition is IEdmCollectionType)
                        yield return item;
                }
        }
        public async Task WriteAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator)
        {
            var resourceSet = new ODataResourceSet() { Count = asyncEnumerator.Count };
            _writer.WriteStart(resourceSet);

            Db.IOeDbEnumerator dbEnumerator = entryFactory.IsTuple ?
                (Db.IOeDbEnumerator)new Db.OeDbEnumerator(asyncEnumerator, entryFactory) : new Db.OeEntityDbEnumerator(asyncEnumerator, entryFactory);

            Object rawValue = null;
            int readCount = 0;
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                await WriteEntry(dbEnumerator, dbEnumerator.Current, _queryContext.NavigationNextLink, _queryContext.ODataUri.SelectAndExpand).ConfigureAwait(false);
                readCount++;
                rawValue = dbEnumerator.RawValue;
                dbEnumerator.ClearBuffer();
            }

            var nextPageLinkBuilder = new OeNextPageLinkBuilder(_queryContext);
            resourceSet.NextPageLink = nextPageLinkBuilder.GetNextPageLinkRoot(entryFactory, readCount, asyncEnumerator.Count, rawValue);

            _writer.WriteEnd();
        }
        private async Task WriteEntry(Db.IOeDbEnumerator dbEnumerator, Object value, bool navigationNextLink, SelectExpandClause selectExpandClause)
        {
            OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
            ODataResource entry = CreateEntry(entryFactory, value);
            _writer.WriteStart(entry);

            for (int i = 0; i < entryFactory.NavigationLinks.Count; i++)
                await WriteNavigation(dbEnumerator.CreateChild(entryFactory.NavigationLinks[i])).ConfigureAwait(false);

            if (navigationNextLink && selectExpandClause != null)
                foreach (ExpandedNavigationSelectItem item in GetExpandedNavigationSelectItems(selectExpandClause))
                    WriteNavigationNextLink(entryFactory, item, value);

            _writer.WriteEnd();
        }
        private async Task WriteNavigation(Db.IOeDbEnumerator dbEnumerator)
        {
            bool isCollection = dbEnumerator.EntryFactory.EdmNavigationProperty.Type.Definition is EdmCollectionType;
            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = isCollection,
                Name = dbEnumerator.EntryFactory.EdmNavigationProperty.Name
            };
            _writer.WriteStart(resourceInfo);

            if (isCollection)
                await WriteNavigationCollection(dbEnumerator).ConfigureAwait(false);
            else
                await WriteNavigationSingle(dbEnumerator);

            _writer.WriteEnd();
        }
        private async Task WriteNavigationCollection(Db.IOeDbEnumerator dbEnumerator)
        {
            Object value;
            int readCount = 0;
            ODataResourceSet resourceSet = null;
            do
            {
                value = dbEnumerator.Current;
                if (value != null)
                {
                    if (resourceSet == null)
                    {
                        resourceSet = new ODataResourceSet();
                        if (dbEnumerator.EntryFactory.NavigationSelectItem.CountOption.GetValueOrDefault())
                            resourceSet.Count = OeNextPageLinkBuilder.GetCount(_queryContext.EdmModel, dbEnumerator.EntryFactory, value);
                        _writer.WriteStart(resourceSet);
                    }

                    SelectExpandClause selectExpandClause = dbEnumerator.EntryFactory.NavigationSelectItem.SelectAndExpand;
                    await WriteEntry(dbEnumerator, value, false, selectExpandClause).ConfigureAwait(false);
                    readCount++;
                }
            }
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

            if (readCount == 0)
            {
                resourceSet = new ODataResourceSet();
                if (dbEnumerator.EntryFactory.NavigationSelectItem.CountOption.GetValueOrDefault())
                    resourceSet.Count = 0;
                _writer.WriteStart(resourceSet);
            }
            else
            {
                var nextPageLinkBuilder = new OeNextPageLinkBuilder(_queryContext);
                resourceSet.NextPageLink = nextPageLinkBuilder.GetNextPageLinkNavigation(dbEnumerator.EntryFactory, readCount, resourceSet.Count, value);
            }

            _writer.WriteEnd();
        }
        private void WriteNavigationNextLink(OeEntryFactory parentEntryFactory, ExpandedNavigationSelectItem item, Object value)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = true,
                Name = segment.NavigationProperty.Name
            };
            _writer.WriteStart(resourceInfo);

            var resourceSet = new ODataResourceSet()
            {
                NextPageLink = OeNextPageLinkBuilder.GetNavigationUri(_queryContext.EdmModel, parentEntryFactory, item, value)
            };
            _writer.WriteStart(resourceSet);
            _writer.WriteEnd();

            _writer.WriteEnd();
        }
        private async Task WriteNavigationSingle(Db.IOeDbEnumerator dbEnumerator)
        {
            Object value = dbEnumerator.Current;
            if (value == null)
            {
                _writer.WriteStart((ODataResource)null);
                _writer.WriteEnd();
            }
            else
            {
                SelectExpandClause selectExpandClause = dbEnumerator.EntryFactory.NavigationSelectItem.SelectAndExpand;
                await WriteEntry(dbEnumerator, value, false, selectExpandClause).ConfigureAwait(false);
            }
        }
    }
}
