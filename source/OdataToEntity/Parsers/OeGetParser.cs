using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public readonly struct OeGetParser
    {
        private readonly IEdmModel _edmModel;
        private readonly Query.OeModelBoundQueryProvider _modelBoundQueryProvider;

        public OeGetParser(IEdmModel edmModel) : this(edmModel, null)
        {
        }
        public OeGetParser(IEdmModel model, Query.OeModelBoundQueryProvider modelBoundQueryProvider)
        {
            _edmModel = model;
            _modelBoundQueryProvider = modelBoundQueryProvider;
        }

        public async Task ExecuteAsync(ODataUri odataUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            var queryContext = new OeQueryContext(_edmModel, odataUri)
            {
                MaxPageSize = headers.MaxPageSize,
                MetadataLevel = headers.MetadataLevel,
                NavigationNextLink = headers.NavigationNextLink,
                ModelBoundQueryProvider = _modelBoundQueryProvider
            };

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
    }
}
