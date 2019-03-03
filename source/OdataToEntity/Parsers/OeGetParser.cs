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

        public OeGetParser(IEdmModel edmModel) : this(edmModel, OdataToEntity.OeModelBoundAttribute.No)
        {
        }
        public OeGetParser(IEdmModel model, OeModelBoundAttribute useModelBoundAttribute)
        {
            _edmModel = model;
            UseModelBoundAttribute = useModelBoundAttribute;
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
        public OeQueryContext CreateQueryContext(ODataUri odataUri, int maxPageSize, bool navigationNextLink, OeMetadataLevel metadataLevel)
        {
            if (maxPageSize > 0)
                odataUri.Top = maxPageSize;

            IReadOnlyList<OeParseNavigationSegment> navigationSegments = OeParseNavigationSegment.GetNavigationSegments(odataUri.Path);
            var entitySetSegment = (EntitySetSegment)odataUri.Path.FirstSegment;
            Db.OeEntitySetAdapter entitySetAdapter = _edmModel.GetEntitySetAdapter(entitySetSegment.EntitySet);
            return new OeQueryContext(_edmModel, odataUri, entitySetAdapter, navigationSegments,
                maxPageSize, navigationNextLink, metadataLevel, UseModelBoundAttribute);
        }
        public async Task ExecuteAsync(ODataUri odataUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            OeQueryContext queryContext = CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
            if (queryContext.ODataUri.Path.LastSegment is OperationSegment)
            {
                using (Db.OeAsyncEnumerator asyncEnumerator = OeOperationHelper.ApplyBoundFunction(queryContext))
                    await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, stream).ConfigureAwait(false);

                return;
            }

            Object dataContext = null;
            Db.OeDataAdapter dataAdapter = queryContext.EdmModel.GetDataAdapter(queryContext.EdmModel.EntityContainer);
            try
            {
                dataContext = dataAdapter.CreateDataContext();
                if (odataUri.Path.LastSegment is CountSegment)
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

        public OeModelBoundAttribute UseModelBoundAttribute { get; }
    }
}
