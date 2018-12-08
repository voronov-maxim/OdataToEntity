using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class ODataResult<T> : IActionResult
    {
        private readonly struct ClrPropertiesInfo
        {
            public ClrPropertiesInfo(PropertyInfo parentPropertyInfo, PropertyInfo[] structurals, ClrPropertiesInfo[] navigations)
            {
                ParentPropertyInfo = parentPropertyInfo;
                Structurals = structurals;
                Navigations = navigations;
                IsCollection = parentPropertyInfo == null ? false : OeExpressionHelper.GetCollectionItemType(parentPropertyInfo.PropertyType) != null;
            }

            public bool IsCollection { get; }
            public ClrPropertiesInfo[] Navigations { get; }
            public PropertyInfo ParentPropertyInfo { get; }
            public PropertyInfo[] Structurals { get; }
        }

        private readonly IEdmModel _edmModel;
        private readonly IAsyncEnumerator<T> _entities;
        private readonly ODataUri _odataUri;
        private readonly IEdmEntitySetBase _resultEntitySet;
        private readonly HashSet<Object> _stack;

        public ODataResult(IEdmModel edmModel, ODataUri odataUri, IEdmEntitySetBase resultEntitySet, IAsyncEnumerator<T> entities)
        {
            _odataUri = odataUri;
            _edmModel = edmModel;
            _resultEntitySet = resultEntitySet;
            _entities = entities;

            _stack = new HashSet<Object>();
        }

        private Uri BuildNextPageLink(String skipToken)
        {
            ODataUri nextOdataUri = _odataUri.Clone();
            nextOdataUri.ServiceRoot = null;
            nextOdataUri.QueryCount = null;
            nextOdataUri.Top = PageSize;
            nextOdataUri.Skip = null;
            nextOdataUri.SkipToken = skipToken;

            return nextOdataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = _odataUri.ServiceRoot,
                EnableMessageStreamDisposal = false,
                ODataUri = _odataUri,
                Validations = ValidationKinds.ThrowIfTypeConflictsWithMetadata | ValidationKinds.ThrowOnDuplicatePropertyNames,
                Version = ODataVersion.V4
            };

            var requestHeaders = OeRequestHeaders.Parse(context.HttpContext.Request.Headers["Accept"], context.HttpContext.Request.Headers["Prefer"]);
            if (requestHeaders.MaxPageSize > 0 && PageSize == 0)
                PageSize = requestHeaders.MaxPageSize;

            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(context.HttpContext.Response.Body, context.HttpContext.Request.ContentType);
            using (ODataMessageWriter messageWriter = new ODataMessageWriter(responseMessage, settings, _edmModel))
            {
                ODataUtils.SetHeadersForPayload(messageWriter, ODataPayloadKind.ResourceSet);

                IEdmEntityType entityType = _resultEntitySet.EntityType();
                ODataWriter writer = messageWriter.CreateODataResourceSetWriter(_resultEntitySet, entityType);
                await SerializeAsync(writer, requestHeaders.MetadataLevel);
            }
        }
        private static ClrPropertiesInfo GetClrPropertiesInfo(IEdmModel edmModel, SelectExpandClause selectAndExpand,
            OeMetadataLevel metadataLevel, Type clrType, PropertyInfo parentPropertyInfo)
        {
            IEnumerable<IEdmStructuralProperty> edmStructuralProperties;
            ClrPropertiesInfo[] clrPropertiesInfos = Array.Empty<ClrPropertiesInfo>();

            var edmEntityType = (IEdmEntityType)edmModel.FindDeclaredType(clrType.FullName);
            if (selectAndExpand == null)
                edmStructuralProperties = edmEntityType.StructuralProperties();
            else
            {
                var structuralPropertyList = new List<IEdmStructuralProperty>();
                var clrPropertiesInfoList = new List<ClrPropertiesInfo>();

                foreach (SelectItem selectItem in selectAndExpand.SelectedItems)
                    if (selectItem is ExpandedNavigationSelectItem expandedNavigationSelectItem)
                    {
                        if (expandedNavigationSelectItem.PathToNavigationProperty.Count > 1)
                            throw new InvalidOperationException("Complex navigation property not supported");

                        var navigationSegment = (NavigationPropertySegment)expandedNavigationSelectItem.PathToNavigationProperty.FirstSegment;
                        AddNavigationProperty(navigationSegment, expandedNavigationSelectItem.SelectAndExpand, clrPropertiesInfoList);
                    }
                    else if (selectItem is PathSelectItem pathSelectItem)
                    {
                        if (pathSelectItem.SelectedPath.Count > 1)
                            throw new InvalidOperationException("Complex structural property not supported");

                        if (pathSelectItem.SelectedPath.FirstSegment is NavigationPropertySegment navigationSegment)
                            AddNavigationProperty(navigationSegment, null, clrPropertiesInfoList);
                        else
                        {
                            var propertySegment = (PropertySegment)pathSelectItem.SelectedPath.FirstSegment;
                            structuralPropertyList.Add(propertySegment.Property);
                        }
                    }

                if (metadataLevel == OeMetadataLevel.Full)
                    foreach (IEdmStructuralProperty keyProperty in edmEntityType.Key())
                        if (!structuralPropertyList.Contains(keyProperty))
                            structuralPropertyList.Add(keyProperty);

                if (selectAndExpand.AllSelected)
                    edmStructuralProperties = edmEntityType.StructuralProperties();
                else
                    edmStructuralProperties = structuralPropertyList.ToArray();
                clrPropertiesInfos = clrPropertiesInfoList.Count == 0 ? Array.Empty<ClrPropertiesInfo>() : clrPropertiesInfoList.ToArray();
            }

            var clrStructuralProperties = new List<PropertyInfo>();
            foreach (IEdmProperty edmProperty in edmStructuralProperties)
                clrStructuralProperties.Add(clrType.GetProperty(edmProperty.Name));

            return new ClrPropertiesInfo(parentPropertyInfo, clrStructuralProperties.ToArray(), clrPropertiesInfos);

            void AddNavigationProperty(NavigationPropertySegment navigationSegment, SelectExpandClause selectExpandClause, List<ClrPropertiesInfo> clrPropertiesInfoList)
            {
                PropertyInfo navigationPropertyInfo = clrType.GetPropertyIgnoreCase(navigationSegment.NavigationProperty);
                Type itemType = OeExpressionHelper.GetCollectionItemType(navigationPropertyInfo.PropertyType);
                if (itemType == null)
                    itemType = navigationPropertyInfo.PropertyType;

                foreach (ClrPropertiesInfo clrPropertiesInfo in clrPropertiesInfoList)
                    if (clrPropertiesInfo.ParentPropertyInfo == navigationPropertyInfo)
                        return;

                clrPropertiesInfoList.Add(GetClrPropertiesInfo(edmModel, selectExpandClause, metadataLevel, itemType, navigationPropertyInfo));
            }
        }
        private IEnumerable<KeyValuePair<String, Object>> GetKeys(T entity)
        {
            var visitor = new OeQueryNodeVisitor(_edmModel, Expression.Parameter(typeof(T)));
            OrderByClause orderByClause = _odataUri.OrderBy;
            do
            {
                var propertyExpression = (MemberExpression)visitor.TranslateNode(orderByClause.Expression);
                UnaryExpression body = Expression.Convert(propertyExpression, typeof(Object));
                Expression<Func<T, Object>> getValueLambda = Expression.Lambda<Func<T, Object>>(body, visitor.Parameter);
                Object value = getValueLambda.Compile()(entity);

                yield return new KeyValuePair<String, Object>(OeSkipTokenParser.GetPropertyName((PropertyInfo)propertyExpression.Member), value);

                orderByClause = orderByClause.ThenBy;
            }
            while (orderByClause != null);
        }
        private async Task SerializeAsync(ODataWriter writer, OeMetadataLevel metadataLevel)
        {
            ClrPropertiesInfo clrPropertiesInfo = GetClrPropertiesInfo(_edmModel, _odataUri.SelectAndExpand, metadataLevel, typeof(T), null);

            var resourceSet = new ODataResourceSet() { Count = Count };
            writer.WriteStart(resourceSet);

            int count = 0;
            T entity = default;
            while (await _entities.MoveNext())
            {
                entity = _entities.Current;
                _stack.Add(entity);
                WriteEntry(writer, entity, clrPropertiesInfo);
                _stack.Remove(entity);
                count++;
            }

            if (PageSize > 0 && count > 0 && (Count ?? Int32.MaxValue) > count)
                resourceSet.NextPageLink = BuildNextPageLink(OeSkipTokenParser.GetSkipToken(_edmModel, GetKeys(entity)));

            writer.WriteEnd();
        }
        private void WriteEntry(ODataWriter writer, Object entity, in ClrPropertiesInfo clrPropertiesInfo)
        {
            ODataResource entry = OeDataContext.CreateEntry(entity, clrPropertiesInfo.Structurals);
            writer.WriteStart(entry);

            foreach (ClrPropertiesInfo navigationPropertiesInfo in clrPropertiesInfo.Navigations)
                WriteNavigationProperty(writer, entity, navigationPropertiesInfo);

            writer.WriteEnd();
        }
        private void WriteNavigationProperty(ODataWriter writer, Object value, in ClrPropertiesInfo navigationPropertiesInfo)
        {
            Object navigationValue = navigationPropertiesInfo.ParentPropertyInfo.GetValue(value);
            if (!_stack.Add(navigationValue))
                return;

            var resourceInfo = new ODataNestedResourceInfo()
            {
                IsCollection = navigationPropertiesInfo.IsCollection,
                Name = navigationPropertiesInfo.ParentPropertyInfo.Name
            };
            writer.WriteStart(resourceInfo);

            if (navigationValue == null)
            {
                if (navigationPropertiesInfo.IsCollection)
                    writer.WriteStart(new ODataResourceSet());
                else
                    writer.WriteStart((ODataResource)null);
                writer.WriteEnd();
            }
            else
            {
                if (navigationPropertiesInfo.IsCollection)
                {
                    writer.WriteStart(new ODataResourceSet());
                    foreach (Object entity in (IEnumerable)navigationValue)
                        WriteEntry(writer, entity, navigationPropertiesInfo);
                    writer.WriteEnd();
                }
                else
                    WriteEntry(writer, navigationValue, navigationPropertiesInfo);
            }

            writer.WriteEnd();
            _stack.Remove(navigationValue);
        }

        public int? Count { get; set; }
        public int PageSize { get; set; }
    }
}