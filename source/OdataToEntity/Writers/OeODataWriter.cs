using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private static Uri BuildNavigationNextPageLink(IEdmModel edmModel, OeEntryFactory entryFactory, ExpandedNavigationSelectItem item,
            OrderByClause orderByClause, Object value, String skipToken)
        {
            SingleValueNode filterExpression;
            ResourceRangeVariableReferenceNode refNode;

            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            IEdmNavigationProperty navigationProperty = segment.NavigationProperty;
            if (navigationProperty.ContainsTarget)
            {
                ModelBuilder.ManyToManyJoinDescription joinDescription = edmModel.GetManyToManyJoinDescription(navigationProperty);
                navigationProperty = joinDescription.JoinNavigationProperty.Partner;

                IEdmEntitySet joinNavigationSource = OeEdmClrHelper.GetEntitySet(edmModel, joinDescription.JoinNavigationProperty);
                ResourceRangeVariableReferenceNode joinRefNode = OeEdmClrHelper.CreateRangeVariableReferenceNode(joinNavigationSource, "d");

                IEdmEntitySet targetNavigationSource = OeEdmClrHelper.GetEntitySet(edmModel, joinDescription.TargetNavigationProperty);
                ResourceRangeVariableReferenceNode targetRefNode = OeEdmClrHelper.CreateRangeVariableReferenceNode(targetNavigationSource);

                var anyNode = new AnyNode(new Collection<RangeVariable>() { joinRefNode.RangeVariable, targetRefNode.RangeVariable }, joinRefNode.RangeVariable)
                {
                    Source = new CollectionNavigationNode(targetRefNode, joinDescription.TargetNavigationProperty.Partner, null),
                    Body = OeGetParser.CreateFilterExpression(joinRefNode, GetKeysFromParentValue(navigationProperty))
                };

                refNode = targetRefNode;
                filterExpression = anyNode;
            }
            else
            {
                IEdmNavigationProperty dependentNavigationProperty = navigationProperty.IsPrincipal() ? navigationProperty.Partner : navigationProperty;

                refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode((IEdmEntitySetBase)segment.NavigationSource);
                List<KeyValuePair<IEdmStructuralProperty, Object>> keys;
                if (entryFactory.EdmNavigationProperty == navigationProperty)
                    keys = GetKeysSelfValue(dependentNavigationProperty);
                else
                    keys = GetKeysFromParentValue(dependentNavigationProperty);
                filterExpression = OeGetParser.CreateFilterExpression(refNode, keys);
            }

            if (item.FilterOption != null)
                filterExpression = new BinaryOperatorNode(BinaryOperatorKind.And, filterExpression, item.FilterOption.Expression);

            long? top = item.TopOption;
            if (top.GetValueOrDefault() > 0)
                if (entryFactory.MaxTop > 0 && entryFactory.MaxTop < top.GetValueOrDefault())
                    top = entryFactory.MaxTop;

            var pathSegments = new ODataPathSegment[] { new EntitySetSegment((IEdmEntitySet)refNode.NavigationSource) };
            var odataUri = new ODataUri()
            {
                Filter = new FilterClause(filterExpression, refNode.RangeVariable),
                OrderBy = orderByClause,
                Path = new ODataPath(pathSegments),
                QueryCount = item.CountOption,
                SelectAndExpand = item.SelectAndExpand,
                Skip = item.SkipOption,
                SkipToken = skipToken,
                Top = top,
            };

            return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);

            List<KeyValuePair<IEdmStructuralProperty, Object>> GetKeysFromParentValue(IEdmNavigationProperty edmNavigationProperty)
            {
                var keys = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
                IEnumerator<IEdmStructuralProperty> dependentProperties = edmNavigationProperty.DependentProperties().GetEnumerator();
                foreach (IEdmStructuralProperty key in edmNavigationProperty.PrincipalProperties())
                {
                    dependentProperties.MoveNext();
                    Object keyValue = entryFactory.GetAccessorByName(key.Name).GetValue(value);
                    keys.Add(new KeyValuePair<IEdmStructuralProperty, Object>(dependentProperties.Current, keyValue));
                }
                return keys;
            }
            List<KeyValuePair<IEdmStructuralProperty, Object>> GetKeysSelfValue(IEdmNavigationProperty edmNavigationProperty)
            {
                var keys = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
                foreach (IEdmStructuralProperty key in edmNavigationProperty.DependentProperties())
                {
                    Object keyValue = entryFactory.GetAccessorByName(key.Name).GetValue(value);
                    keys.Add(new KeyValuePair<IEdmStructuralProperty, Object>(key, keyValue));
                }
                return keys;
            }
        }
        private static Uri BuildNextPageLink(OeQueryContext queryContext, OePropertyAccessor[] skipTokenAccessors, Object value, int pageSize, int? restCount)
        {
            ODataUri nextOdataUri = queryContext.ODataUri.Clone();
            nextOdataUri.OrderBy = queryContext.ODataUri.OrderBy;
            nextOdataUri.ServiceRoot = null;
            nextOdataUri.QueryCount = null;
            nextOdataUri.Top = pageSize;
            nextOdataUri.Skip = null;
            nextOdataUri.SkipToken = OeSkipTokenParser.GetSkipToken(queryContext.EdmModel, skipTokenAccessors, value, restCount);

            return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }
        private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
        {
            ODataResource entry = entryFactory.CreateEntry(entity);
            if (_queryContext.MetadataLevel == OeMetadataLevel.Full)
                entry.Id = OeUriHelper.ComputeId(_queryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
            return entry;
        }
        private static ExpandedNavigationSelectItem GetExpandedNavigationSelectItem(OeEntryFactory entryFactory, SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause != null && selectExpandClause.SelectedItems != null)
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem item)
                    {
                        if (item.PathToNavigationProperty.LastSegment.Identifier == entryFactory.ResourceInfo.Name)
                            return item;
                    }
                    else if (selectItem is PathSelectItem pathSelectItem && pathSelectItem.SelectedPath.LastSegment is NavigationPropertySegment segment)
                    {
                        if (entryFactory.PageSize > 0 && segment.Identifier == entryFactory.ResourceInfo.Name)
                            return new ExpandedNavigationSelectItem(new ODataExpandPath(segment), null, null, null, null, entryFactory.PageSize, null, null, null, null);
                    }

            if (entryFactory.PageSize > 0)
            {
                var segment = new NavigationPropertySegment(entryFactory.EdmNavigationProperty, entryFactory.EntitySet);
                return new ExpandedNavigationSelectItem(new ODataExpandPath(segment), null, null, null, null, entryFactory.PageSize, null, null, null, null);
            }

            return null;
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
        private static KeyValuePair<String, Object>[] GetNestedSkipTokenKeys(OeEntryFactory entryFactory, OrderByClause orderByClause, Object value)
        {
            IEdmStructuralProperty[] keyProperties = OeSkipTokenParser.GetEdmProperies(orderByClause);
            var keys = new KeyValuePair<String, Object>[keyProperties.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                String propertyName = OeSkipTokenParser.GetPropertyName(keyProperties[i]);
                OePropertyAccessor accessor = entryFactory.GetAccessorByName(keyProperties[i].Name);
                keys[i] = new KeyValuePair<String, Object>(propertyName, accessor.GetValue(value));
            }
            return keys;
        }
        private int GetPageSize(OeEntryFactory entryFactory)
        {
            int pageSize = entryFactory.PageSize;
            if (pageSize == 0)
            {
                pageSize = _queryContext.MaxPageSize;
                if (pageSize == 0 && _queryContext.ODataUri.SkipToken != null && _queryContext.ODataUri.Top.GetValueOrDefault() > 0)
                    pageSize = (int)_queryContext.ODataUri.Top.GetValueOrDefault();
            }
            else
            {
                if (_queryContext.MaxPageSize > 0 && _queryContext.MaxPageSize < pageSize)
                    pageSize = _queryContext.MaxPageSize;
            }

            return pageSize;
        }
        private int? GetRestCount(Db.OeAsyncEnumerator asyncEnumerator, int pageSize, int readCount)
        {
            int? restCount = asyncEnumerator.Count ?? _queryContext.RestCount;
            if (pageSize > 0 && readCount > 0 && (restCount == null || restCount.GetValueOrDefault() > 0))
            {
                if (restCount == null)
                {
                    var top = (int)_queryContext.ODataUri.Top.GetValueOrDefault();
                    if (top > 0 && _queryContext.ODataUri.SkipToken == null)
                    {
                        if (_queryContext.EntryFactory.MaxTop > 0)
                        {
                            if (_queryContext.EntryFactory.MaxTop < top)
                                top = _queryContext.EntryFactory.MaxTop;
                            restCount = top - readCount;
                        }
                        else
                            restCount = Int32.MaxValue;
                    }
                    else
                        throw new InvalidOperationException("Rest row count must by set in $skiptoken");
                }
                else
                    restCount -= readCount;

                return restCount;
            }

            return 0;
        }
        public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator)
        {
            var resourceSet = new ODataResourceSet() { Count = asyncEnumerator.Count };
            _writer.WriteStart(resourceSet);

            Db.IOeDbEnumerator dbEnumerator = entryFactory.IsTuple ?
                (Db.IOeDbEnumerator)new Db.OeDbEnumerator(asyncEnumerator, entryFactory) : new Db.OeEntityDbEnumerator(asyncEnumerator, entryFactory);

            Object buffer = null;
            int readCount = 0;
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                await WriteEntry(dbEnumerator, dbEnumerator.Current, _queryContext.NavigationNextLink, _queryContext.ODataUri.SelectAndExpand).ConfigureAwait(false);
                readCount++;
                buffer = dbEnumerator.ClearBuffer();
            }

            int pageSize = GetPageSize(entryFactory);
            int? restCount = GetRestCount(asyncEnumerator, pageSize, readCount);
            if (restCount > 0)
                resourceSet.NextPageLink = BuildNextPageLink(_queryContext, entryFactory.SkipTokenAccessors, buffer, pageSize, restCount);

            _writer.WriteEnd();
        }
        private async Task WriteEagerNestedCollection(Db.IOeDbEnumerator dbEnumerator, ExpandedNavigationSelectItem item)
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
                await WriteEntry(dbEnumerator, items[i], false, item.SelectAndExpand).ConfigureAwait(false);
            _writer.WriteEnd();
        }
        private async Task WriteEntry(Db.IOeDbEnumerator dbEnumerator, Object value, bool navigationNextLink, SelectExpandClause selectExpandClause)
        {
            OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
            ODataResource entry = CreateEntry(entryFactory, value);
            _writer.WriteStart(entry);

            for (int i = 0; i < entryFactory.NavigationLinks.Count; i++)
            {
                ExpandedNavigationSelectItem item = GetExpandedNavigationSelectItem(entryFactory.NavigationLinks[i], selectExpandClause);
                await WriteNavigationLink(dbEnumerator.CreateChild(entryFactory.NavigationLinks[i]), item).ConfigureAwait(false);
            }

            if (navigationNextLink)
                foreach (ExpandedNavigationSelectItem item in GetExpandedNavigationSelectItems(selectExpandClause))
                    WriteNavigationNextLink(entryFactory, item, value);

            _writer.WriteEnd();
        }
        private async Task WriteLazyNestedCollection(Db.IOeDbEnumerator dbEnumerator, ExpandedNavigationSelectItem item)
        {
            var resourceSet = new ODataResourceSet();
            _writer.WriteStart(resourceSet);
            Object value;
            int readCount = 0;
            do
            {
                value = dbEnumerator.Current;
                if (value != null)
                {
                    await WriteEntry(dbEnumerator, value, false, item?.SelectAndExpand).ConfigureAwait(false);
                    readCount++;
                }
            }
            while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

            OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
            if (item != null && entryFactory.PageSize > 0 && readCount > 0)
            {
                OrderByClause orderByClause = OeSkipTokenParser.GetUniqueOrderBy(_queryContext.EdmModel, entryFactory.EntitySet, item.OrderByOption, null);
                KeyValuePair<String, Object>[] keys = GetNestedSkipTokenKeys(entryFactory, orderByClause, value);

                int? restCount = null;
                if (entryFactory.MaxTop > 0)
                    restCount = entryFactory.MaxTop - readCount;

                String skipToken = OeSkipTokenParser.GetSkipToken(_queryContext.EdmModel, keys, restCount);
                resourceSet.NextPageLink = BuildNavigationNextPageLink(_queryContext.EdmModel, entryFactory, item, orderByClause, value, skipToken);
            }

            _writer.WriteEnd();
        }
        private async Task WriteNavigationLink(Db.IOeDbEnumerator dbEnumerator, ExpandedNavigationSelectItem item)
        {
            _writer.WriteStart(dbEnumerator.EntryFactory.ResourceInfo);
            if (dbEnumerator.EntryFactory.ResourceInfo.IsCollection.GetValueOrDefault())
            {
                if (dbEnumerator.EntryFactory.CountOption.GetValueOrDefault())
                    await WriteEagerNestedCollection(dbEnumerator, item).ConfigureAwait(false);
                else
                    await WriteLazyNestedCollection(dbEnumerator, item).ConfigureAwait(false);
            }
            else
                await WriteNestedItem(dbEnumerator, item);
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

            var resourceSet = new ODataResourceSet()
            {
                NextPageLink = BuildNavigationNextPageLink(_queryContext.EdmModel, entryFactory, item, item.OrderByOption, value, null)
            };
            _writer.WriteStart(resourceSet);
            _writer.WriteEnd();

            _writer.WriteEnd();
        }
        private async Task WriteNestedItem(Db.IOeDbEnumerator dbEnumerator, ExpandedNavigationSelectItem item)
        {
            Object value = dbEnumerator.Current;
            if (value == null)
            {
                _writer.WriteStart((ODataResource)null);
                _writer.WriteEnd();
            }
            else
                await WriteEntry(dbEnumerator, value, false, item?.SelectAndExpand).ConfigureAwait(false);
        }
    }
}
