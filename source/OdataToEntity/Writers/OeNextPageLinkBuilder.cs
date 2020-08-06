using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OdataToEntity.Writers
{
    public readonly struct OeNextPageLinkBuilder
    {
        private readonly OeQueryContext _queryContext;

        public OeNextPageLinkBuilder(OeQueryContext queryContext)
        {
            _queryContext = queryContext;
        }

        private static ODataUri GetCountODataUri(IEdmModel edmModel, OeEntryFactory entryFactory, ExpandedNavigationSelectItem item, Object? value)
        {
            FilterClause? filterClause = GetFilter(edmModel, entryFactory, item, value);
            if (filterClause == null)
                throw new InvalidOperationException("Cannot create count expression");

            var entitytSet = (IEdmEntitySet)((ResourceRangeVariable)filterClause.RangeVariable).NavigationSource;
            var pathSegments = new ODataPathSegment[] { new EntitySetSegment(entitytSet) { Identifier = entitytSet.Name }, CountSegment.Instance };

            return new ODataUri()
            {
                Filter = filterClause,
                Path = new ODataPath(pathSegments),
            };
        }
        private static FilterClause? GetFilter(IEdmModel edmModel, OeEntryFactory entryFactory, ExpandedNavigationSelectItem item, Object? value)
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
                    Body = OeExpressionHelper.CreateFilterExpression(joinRefNode, GetKeys(navigationProperty.PrincipalProperties(), navigationProperty.DependentProperties()))
                };

                refNode = targetRefNode;
                filterExpression = anyNode;
            }
            else
            {
                IEnumerable<IEdmStructuralProperty> parentKeys;
                IEnumerable<IEdmStructuralProperty> childKeys;
                if (navigationProperty.IsPrincipal())
                {
                    parentKeys = navigationProperty.Partner.PrincipalProperties();
                    childKeys = navigationProperty.Partner.DependentProperties();
                }
                else
                {
                    if (navigationProperty.Type.IsCollection())
                    {
                        parentKeys = navigationProperty.PrincipalProperties();
                        childKeys = navigationProperty.DependentProperties();
                    }
                    else
                    {
                        parentKeys = navigationProperty.DependentProperties();
                        childKeys = navigationProperty.PrincipalProperties();
                    }
                }

                refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode((IEdmEntitySetBase)segment.NavigationSource);
                List<KeyValuePair<IEdmStructuralProperty, Object?>> keys = GetKeys(parentKeys, childKeys);
                if (IsNullKeys(keys))
                    return null;

                filterExpression = OeExpressionHelper.CreateFilterExpression(refNode, keys);
            }

            if (item.FilterOption != null)
                filterExpression = new BinaryOperatorNode(BinaryOperatorKind.And, filterExpression, item.FilterOption.Expression);

            return new FilterClause(filterExpression, refNode.RangeVariable);

            List<KeyValuePair<IEdmStructuralProperty, Object?>> GetKeys(IEnumerable<IEdmStructuralProperty> parentKeys, IEnumerable<IEdmStructuralProperty> childKeys)
            {
                var keys = new List<KeyValuePair<IEdmStructuralProperty, Object?>>();
                IEnumerator<IEdmStructuralProperty> childKeyEnumerator = childKeys.GetEnumerator();
                foreach (IEdmStructuralProperty parentKey in parentKeys)
                {
                    childKeyEnumerator.MoveNext();
                    Object? keyValue = entryFactory.GetAccessorByName(parentKey.Name).GetValue(value);
                    keys.Add(new KeyValuePair<IEdmStructuralProperty, Object?>(childKeyEnumerator.Current, keyValue));
                }
                return keys;
            }
            static bool IsNullKeys(List<KeyValuePair<IEdmStructuralProperty, Object?>> keys)
            {
                foreach (KeyValuePair<IEdmStructuralProperty, Object?> key in keys)
                    if (key.Value != null)
                        return false;
                return true;
            }
        }
        private static KeyValuePair<String, Object?>[] GetNavigationSkipTokenKeys(OeEntryFactory entryFactory, OrderByClause orderByClause, Object value)
        {
            IEdmStructuralProperty[] keyProperties = OeSkipTokenParser.GetEdmProperies(orderByClause);
            var keys = new KeyValuePair<String, Object?>[keyProperties.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                String propertyName = OeSkipTokenParser.GetPropertyName(keyProperties[i]);
                OePropertyAccessor accessor = entryFactory.GetAccessorByName(keyProperties[i].Name);
                keys[i] = new KeyValuePair<String, Object?>(propertyName, accessor.GetValue(value));
            }
            return keys;
        }
        public Uri? GetNavigationUri(OeEntryFactory entryFactory, ExpandedNavigationSelectItem item, Object? value)
        {
            return GetNavigationUri(entryFactory, item, item.OrderByOption, value, null);
        }
        private Uri? GetNavigationUri(OeEntryFactory entryFactory, ExpandedNavigationSelectItem item, OrderByClause orderByClause, Object? value, String? skipToken)
        {
            bool? queryCount = item.CountOption;
            long? top = item.TopOption;
            if (skipToken != null)
            {
                queryCount = null;
                int pageSize = item.GetPageSize();
                if (pageSize > 0 && (top == null || pageSize < top.GetValueOrDefault()))
                    top = pageSize;
            }

            FilterClause? filterClause = GetFilter(_queryContext.EdmModel, entryFactory, item, value);
            if (filterClause == null)
                return null;

            var entitytSet = (IEdmEntitySet)((ResourceRangeVariable)filterClause.RangeVariable).NavigationSource;
            var pathSegments = new ODataPathSegment[] { new EntitySetSegment(entitytSet) };

            var odataUri = new ODataUri()
            {
                Filter = filterClause,
                OrderBy = orderByClause,
                Path = new ODataPath(pathSegments),
                QueryCount = queryCount,
                SelectAndExpand = item.SelectAndExpand,
                Skip = item.SkipOption,
                SkipToken = skipToken,
                Top = top
            };
            return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }
        public static int GetNestedCount(IEdmModel edmModel, Db.IOeDbEnumerator dbEnumerator)
        {
            Db.IOeDbEnumerator? parentEnumerator = dbEnumerator.ParentEnumerator;
            if (parentEnumerator == null)
                throw new InvalidOperationException("For nested count must be child " + nameof(dbEnumerator));

            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            ODataUri odataUri = OeNextPageLinkBuilder.GetCountODataUri(edmModel, parentEnumerator.EntryFactory, entryFactory.NavigationSelectItem, parentEnumerator.Current);
            var queryContext = new OeQueryContext(edmModel, odataUri);

            IEdmEntitySet entitySet = ((EntitySetSegment)odataUri.Path.FirstSegment).EntitySet;
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(entitySet.Container);
            Object? dataContext = null;
            try
            {
                dataContext = dataAdapter.CreateDataContext();
                return dataAdapter.ExecuteScalar<int>(dataContext, queryContext);
            }
            finally
            {
                if (dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
            }
        }
        public Uri? GetNextPageLinkNavigation(Db.IOeDbEnumerator dbEnumerator, int readCount, long? totalCount, Object value)
        {
            var entryFactory = (OeNavigationEntryFactory)dbEnumerator.EntryFactory;
            ExpandedNavigationSelectItem navigationSelectItem = entryFactory.NavigationSelectItem;

            if (navigationSelectItem.GetPageSize() == 0 || readCount == 0 || (totalCount != null && readCount >= totalCount))
                return null;

            OrderByClause orderByClause = OeSkipTokenParser.GetUniqueOrderBy(entryFactory.EntitySet, navigationSelectItem.OrderByOption, null);
            KeyValuePair<String, Object?>[] keys = GetNavigationSkipTokenKeys(entryFactory, orderByClause, value);

            int restCount = GetRestCountNavigation((int?)navigationSelectItem.TopOption, readCount, totalCount);
            String skipToken = OeSkipTokenParser.GetSkipToken(_queryContext.EdmModel, keys, restCount);

            Db.IOeDbEnumerator? parentEnumerator = dbEnumerator.ParentEnumerator;
            if (parentEnumerator == null)
                throw new InvalidOperationException("For nested navigation must be child " + nameof(dbEnumerator));

            return GetNavigationUri(parentEnumerator.EntryFactory, navigationSelectItem, orderByClause, parentEnumerator.Current, skipToken);
        }
        public Uri? GetNextPageLinkRoot(OeEntryFactory entryFactory, int readCount, int? totalCount, Object value)
        {
            if (readCount == 0)
                return null;

            int pageSize = GetPageSizeRoot();
            if (pageSize == 0)
                return null;

            int restCount = GetRestCountRoot(readCount, totalCount);
            if (restCount == 0)
                return null;

            ODataUri nextOdataUri = _queryContext.ODataUri.Clone();
            nextOdataUri.Compute = _queryContext.ODataUri.Compute;
            nextOdataUri.SelectAndExpand = _queryContext.ODataUri.SelectAndExpand;
            nextOdataUri.ServiceRoot = null;
            nextOdataUri.QueryCount = null;
            nextOdataUri.Top = pageSize;
            nextOdataUri.Skip = null;
            nextOdataUri.SkipToken = OeSkipTokenParser.GetSkipToken(_queryContext.EdmModel, entryFactory.SkipTokenAccessors, value, restCount);

            return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }
        private int GetPageSizeRoot()
        {
            int pageSize = _queryContext.ODataUri.GetPageSize();
            if (pageSize == 0)
            {
                if (_queryContext.ODataUri.SkipToken != null && _queryContext.ODataUri.Top.GetValueOrDefault() > 0)
                    pageSize = (int)_queryContext.ODataUri.Top.GetValueOrDefault();
            }

            return pageSize;
        }
        private static int GetRestCountNavigation(int? top, int readCount, long? totalCount)
        {
            if (totalCount != null && totalCount < top.GetValueOrDefault())
                top = (int)totalCount.GetValueOrDefault();

            return (top ?? Int32.MaxValue) - readCount;
        }
        private int GetRestCountRoot(int readCount, int? totalCount)
        {
            int? restCount;
            if (totalCount == null)
                restCount = _queryContext.RestCount;
            else
                restCount = totalCount;

            if (readCount > 0 && (restCount == null || restCount.GetValueOrDefault() > 0))
            {
                if (restCount == null)
                {
                    if (_queryContext.ODataUri.Top.GetValueOrDefault() > 0)
                        restCount = (int)_queryContext.ODataUri.Top.GetValueOrDefault() - readCount;
                    else
                        restCount = Int32.MaxValue;
                }
                else
                    restCount = restCount.GetValueOrDefault() - readCount;
            }

            return restCount.GetValueOrDefault();
        }
    }
}
