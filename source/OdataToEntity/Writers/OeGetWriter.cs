using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            private static Uri BuildNextPageLink(OeQueryContext queryContext, int pageSize, Object value, int? restCount)
            {
                ODataUri nextOdataUri = queryContext.ODataUri.Clone();
                nextOdataUri.ServiceRoot = null;
                nextOdataUri.QueryCount = null;
                nextOdataUri.Top = pageSize;
                nextOdataUri.Skip = null;
                nextOdataUri.SkipToken = OeSkipTokenParser.GetSkipToken(queryContext.EdmModel, queryContext.SkipTokenAccessors, value, restCount);

                return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
            }
            private ODataResource CreateEntry(OeEntryFactory entryFactory, Object entity)
            {
                ODataResource entry = entryFactory.CreateEntry(entity);
                if (_queryContext.MetadataLevel == OeMetadataLevel.Full)
                    entry.Id = OeUriHelper.ComputeId(_queryContext.ODataUri.ServiceRoot, entryFactory.EntitySet, entry);
                return entry;
            }
            private static ExpandedNavigationSelectItem GetExpandedNavigationSelectItem(OeEntryFactory childEntryFactory, SelectExpandClause selectExpandClause)
            {
                if (selectExpandClause != null && selectExpandClause.SelectedItems != null)
                    foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                        if (selectItem is ExpandedNavigationSelectItem item)
                        {
                            if (item.PathToNavigationProperty.LastSegment.Identifier == childEntryFactory.ResourceInfo.Name)
                                return item;
                        }
                        else if (selectItem is PathSelectItem pathSelectItem && pathSelectItem.SelectedPath.LastSegment is NavigationPropertySegment segment)
                        {
                            if (childEntryFactory.PageSize > 0 && segment.Identifier == childEntryFactory.ResourceInfo.Name)
                                return new ExpandedNavigationSelectItem(new ODataExpandPath(segment), null, null, null, null, childEntryFactory.PageSize, null, null, null, null);
                        }

                if (childEntryFactory.PageSize > 0)
                {
                    var segment = new NavigationPropertySegment(childEntryFactory.EdmNavigationProperty, childEntryFactory.EntitySet);
                    return new ExpandedNavigationSelectItem(new ODataExpandPath(segment), null, null, null, null, childEntryFactory.PageSize, null, null, null, null);
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
            public async Task SerializeAsync(OeEntryFactory entryFactory, Db.OeAsyncEnumerator asyncEnumerator, OeQueryContext queryContext)
            {
                var resourceSet = new ODataResourceSet() { Count = asyncEnumerator.Count };
                _writer.WriteStart(resourceSet);

                Object buffer = null;
                int count = 0;
                var dbEnumerator = entryFactory.IsTuple ?
                    (Db.IOeDbEnumerator)new Db.OeDbEnumerator(asyncEnumerator, entryFactory) : new Db.OeEntityDbEnumerator(asyncEnumerator, entryFactory);
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    await WriteEntry(dbEnumerator, dbEnumerator.Current, _queryContext.NavigationNextLink, _queryContext.ODataUri.SelectAndExpand).ConfigureAwait(false);
                    count++;
                    buffer = dbEnumerator.ClearBuffer();
                }

                if (queryContext.RestCount != null)
                    queryContext.RestCount -= count;

                int pageSize = queryContext.MaxPageSize;
                if (entryFactory.PageSize > 0 && (entryFactory.PageSize < pageSize || pageSize == 0))
                    pageSize = entryFactory.PageSize;

                if (pageSize == 0 && _queryContext.ODataUri.SkipToken != null && _queryContext.ODataUri.Top.GetValueOrDefault() > 0)
                    pageSize = (int)_queryContext.ODataUri.Top.GetValueOrDefault();

                if (pageSize > 0 && count > 0 && (asyncEnumerator.Count ?? Int32.MaxValue) > count)
                    if (queryContext.RestCount == null || queryContext.RestCount.GetValueOrDefault() > 0)
                    {
                        int? restCount;
                        var top = (int)queryContext.ODataUri.Top.GetValueOrDefault();
                        if (top > 0 && queryContext.ODataUri.SkipToken == null)
                        {
                            if (queryContext.EntryFactory.MaxTop > 0 && queryContext.EntryFactory.MaxTop < top)
                                top = queryContext.EntryFactory.MaxTop;

                            restCount = top - count;
                        }
                        else
                            restCount = queryContext.RestCount;

                        resourceSet.NextPageLink = BuildNextPageLink(queryContext, pageSize, buffer, restCount);
                    }

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
            private async Task<ODataResource> WriteEntry(Db.IOeDbEnumerator dbEnumerator, Object value, bool navigationNextLink, SelectExpandClause selectExpandClause)
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

                return entry;
            }
            private async Task WriteLazyNestedCollection(Db.IOeDbEnumerator dbEnumerator, ExpandedNavigationSelectItem item)
            {
                var resourceSet = new ODataResourceSet();
                _writer.WriteStart(resourceSet);
                Object value;
                ODataResource entry = null;
                int count = 0;
                do
                {
                    value = dbEnumerator.Current;
                    if (value != null)
                    {
                        entry = await WriteEntry(dbEnumerator, value, false, item?.SelectAndExpand).ConfigureAwait(false);
                        count++;
                    }
                }
                while (await dbEnumerator.MoveNextAsync().ConfigureAwait(false));

                OeEntryFactory entryFactory = dbEnumerator.EntryFactory;
                if (entryFactory.PageSize > 0)
                {
                    OrderByClause orderByClause = OeSkipTokenParser.GetUniqueOrderBy(_queryContext.EdmModel, entryFactory.EntitySet, item.OrderByOption, null);
                    IEdmStructuralProperty[] keyProperties = OeSkipTokenParser.GetEdmProperies(orderByClause);
                    var keys = new KeyValuePair<String, Object>[keyProperties.Length];

                    var properties = (ODataProperty[])entry.Properties;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        ODataProperty property = Array.Find(properties, p => p.Name == keyProperties[i].Name);
                        String propertyName = OeSkipTokenParser.GetPropertyName(keyProperties[i]);
                        keys[i] = new KeyValuePair<String, Object>(propertyName, property.Value);
                    }

                    int? restCount = null;
                    if (entryFactory.MaxTop > 0)
                        restCount = entryFactory.MaxTop - count;

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

        public static async Task SerializeAsync(OeQueryContext queryContext, Db.OeAsyncEnumerator asyncEnumerator, String contentType, Stream stream)
        {
            OeEntryFactory entryFactory = queryContext.EntryFactory;
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
                var getWriter = new GetWriter(queryContext, writer);
                await getWriter.SerializeAsync(entryFactory, asyncEnumerator, queryContext).ConfigureAwait(false);
            }
        }
    }
}

