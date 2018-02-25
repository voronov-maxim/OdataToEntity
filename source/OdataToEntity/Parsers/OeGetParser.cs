using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity
{
    public struct OeGetParser
    {
        private readonly IEdmModel _edmModel;
        private readonly Db.OeDataAdapter _dataAdapter;

        public OeGetParser(Db.OeDataAdapter dataAdapter, IEdmModel model)
        {
            _dataAdapter = dataAdapter;
            _edmModel = model;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            var entityTypeRef = (IEdmEntityTypeReference)((IEdmCollectionType)entitySet.Type).ElementType;
            var range = new ResourceRangeVariable("", entityTypeRef, entitySet);
            var refNode = new ResourceRangeVariableReferenceNode("$it", range);

            BinaryOperatorNode compositeNode = null;
            var entityType = (IEdmEntityType)entityTypeRef.Definition;
            foreach (KeyValuePair<String, Object> keyValue in keys)
            {
                IEdmProperty property = entityType.FindProperty(keyValue.Key);
                var left = new SingleValuePropertyAccessNode(refNode, property);
                var right = new ConstantNode(keyValue.Value, ODataUriUtils.ConvertToUriLiteral(keyValue.Value, ODataVersion.V4));
                var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);

                if (compositeNode == null)
                    compositeNode = node;
                else
                    compositeNode = new BinaryOperatorNode(BinaryOperatorKind.And, compositeNode, node);
            }
            return new FilterClause(compositeNode, range);
        }
        public OeQueryContext CreateQueryContext(ODataUri odataUri, int pageSize, bool navigationNextLink, OeMetadataLevel metadataLevel)
        {
            List<OeParseNavigationSegment> navigationSegments = null;
            if (odataUri.Path.LastSegment is KeySegment ||
                odataUri.Path.LastSegment is NavigationPropertySegment)
            {
                navigationSegments = new List<OeParseNavigationSegment>();
                ODataPathSegment previousSegment = null;
                foreach (ODataPathSegment segment in odataUri.Path)
                {
                    if (segment is NavigationPropertySegment)
                    {
                        var navigationSegment = segment as NavigationPropertySegment;
                        if (navigationSegment == odataUri.Path.LastSegment)
                            navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                        else
                            navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                    }
                    else if (segment is KeySegment)
                    {
                        IEdmEntitySet previousEntitySet;
                        var keySegment = segment as KeySegment;
                        NavigationPropertySegment navigationSegment = null;
                        if (previousSegment is EntitySetSegment)
                        {
                            var previousEntitySetSegment = previousSegment as EntitySetSegment;
                            previousEntitySet = previousEntitySetSegment.EntitySet;
                        }
                        else if (previousSegment is NavigationPropertySegment)
                        {
                            navigationSegment = previousSegment as NavigationPropertySegment;
                            previousEntitySet = (IEdmEntitySet)navigationSegment.NavigationSource;
                        }
                        else
                            throw new InvalidOperationException("invalid segment");

                        FilterClause keyFilter = CreateFilterClause(previousEntitySet, keySegment.Keys);
                        navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, keyFilter));
                    }
                    previousSegment = segment;
                }
            }

            if (pageSize > 0)
            {
                odataUri.Top = pageSize;
                IEdmEntityType edmEntityType = GetEntityType(odataUri.Path, navigationSegments);
                odataUri.OrderBy = OeSkipTokenParser.GetUniqueOrderBy(_edmModel, edmEntityType, odataUri.OrderBy);
            }

            var entitySetSegment = (EntitySetSegment)odataUri.Path.FirstSegment;
            IEdmEntitySet entitySet = entitySetSegment.EntitySet;
            Db.OeEntitySetAdapter entitySetAdapter = _dataAdapter.GetEntitySetAdapter(entitySet.Name);
            bool isCountSegment = odataUri.Path.LastSegment is CountSegment;
            return new OeQueryContext(_edmModel, odataUri, entitySet, navigationSegments,
                isCountSegment, pageSize, navigationNextLink, _dataAdapter.IsDatabaseNullHighestValue, metadataLevel, ref entitySetAdapter);
        }
        public async Task ExecuteAsync(ODataUri odataUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            Object dataContext = null;
            try
            {
                dataContext = _dataAdapter.CreateDataContext();
                OeQueryContext queryContext = CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
                if (queryContext.IsCountSegment)
                {
                    headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                    int count = _dataAdapter.ExecuteScalar<int>(dataContext, queryContext);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(count.ToString(CultureInfo.InvariantCulture));
                    stream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    using (Db.OeAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteEnumerator(dataContext, queryContext, cancellationToken))
                        await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, stream).ConfigureAwait(false);
                }
            }
            finally
            {
                if (dataContext != null)
                    _dataAdapter.CloseDataContext(dataContext);
            }
        }
        internal static IEdmEntityType GetEntityType(ODataPath odataPath, IReadOnlyList<OeParseNavigationSegment> navigationSegments)
        {
            var entitySetSegment = (EntitySetSegment)odataPath.FirstSegment;
            IEdmEntityType entityType = entitySetSegment.EntitySet.EntityType();
            if (navigationSegments != null)
                for (int i = navigationSegments.Count - 1; i >= 0; i--)
                    if (navigationSegments[i].NavigationSegment != null)
                    {
                        entityType = navigationSegments[i].NavigationSegment.NavigationSource.EntityType();
                        break;
                    }
            return entityType;
        }
    }
}
