using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OeGetParser
    {
        private readonly IEdmModel _edmModel;

        public OeGetParser(IEdmModel model)
        {
            _edmModel = model;
        }

        private static FilterClause CreateFilterClause(IEdmEntitySet entitySet, IEnumerable<KeyValuePair<String, Object>> keys)
        {
            ResourceRangeVariableReferenceNode refNode = OeEdmClrHelper.CreateRangeVariableReferenceNode(entitySet);
            var entityType = (IEdmEntityType)refNode.RangeVariable.TypeReference.Definition;

            var propertyValues = new List<KeyValuePair<IEdmStructuralProperty, Object>>();
            foreach (KeyValuePair<String, Object> keyValue in keys)
            {
                var property = (IEdmStructuralProperty)entityType.FindProperty(keyValue.Key);
                propertyValues.Add(new KeyValuePair<IEdmStructuralProperty, Object>(property, keyValue.Value));
            }

            return new FilterClause(CreateFilterExpression(refNode, propertyValues), refNode.RangeVariable);
        }
        internal static BinaryOperatorNode CreateFilterExpression(SingleValueNode singleValueNode, IEnumerable<KeyValuePair<IEdmStructuralProperty, Object>> keys)
        {
            BinaryOperatorNode compositeNode = null;
            foreach (KeyValuePair<IEdmStructuralProperty, Object> keyValue in keys)
            {
                var left = new SingleValuePropertyAccessNode(singleValueNode, keyValue.Key);
                var right = new ConstantNode(keyValue.Value, ODataUriUtils.ConvertToUriLiteral(keyValue.Value, ODataVersion.V4));
                var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);

                if (compositeNode == null)
                    compositeNode = node;
                else
                    compositeNode = new BinaryOperatorNode(BinaryOperatorKind.And, compositeNode, node);
            }
            return compositeNode;
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
                    if (segment is NavigationPropertySegment navigationSegment)
                        navigationSegments.Add(new OeParseNavigationSegment(navigationSegment, null));
                    else if (segment is KeySegment keySegment)
                    {
                        IEdmEntitySet previousEntitySet;
                        navigationSegment = null;
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

            var entitySetSegment = (EntitySetSegment)odataUri.Path.FirstSegment;
            if (pageSize > 0)
            {
                odataUri.Top = pageSize;
                IEdmEntitySet resultEntitySet = OeParseNavigationSegment.GetEntitySet(navigationSegments) ?? entitySetSegment.EntitySet;
                odataUri.OrderBy = OeSkipTokenParser.GetUniqueOrderBy(_edmModel, resultEntitySet, odataUri.OrderBy, odataUri.Apply);
            }

            Db.OeEntitySetAdapter entitySetAdapter = _edmModel.GetEntitySetAdapter(entitySetSegment.EntitySet);
            bool isCountSegment = odataUri.Path.LastSegment is CountSegment;
            return new OeQueryContext(_edmModel, odataUri, navigationSegments, isCountSegment, pageSize, navigationNextLink, metadataLevel, entitySetAdapter);
        }
        public async Task ExecuteAsync(ODataUri odataUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            OeQueryContext queryContext = CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
            Db.OeDataAdapter dataAdapter = _edmModel.GetDataAdapter(queryContext.EdmModel.EntityContainer);

            Object dataContext = null;
            try
            {
                dataContext = dataAdapter.CreateDataContext();
                if (queryContext.IsCountSegment)
                {
                    headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                    int count = dataAdapter.ExecuteScalar<int>(dataContext, queryContext);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(count.ToString(CultureInfo.InvariantCulture));
                    stream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    using (Db.OeAsyncEnumerator asyncEnumerator = dataAdapter.ExecuteEnumerator(dataContext, queryContext, cancellationToken))
                        await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, stream).ConfigureAwait(false);
                }
            }
            finally
            {
                if (dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
            }
        }
    }
}
