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
        private readonly Query.OeModelBoundProvider _modelBoundProvider;

        public OeGetParser(IEdmModel edmModel) : this(edmModel, null)
        {
        }
        public OeGetParser(IEdmModel model, Query.OeModelBoundProvider modelBoundProvider)
        {
            _edmModel = model;
            _modelBoundProvider = modelBoundProvider;
        }

        public async Task ExecuteAsync(ODataUri odataUri, OeRequestHeaders headers, Stream stream, CancellationToken cancellationToken)
        {
            if (_modelBoundProvider != null)
                _modelBoundProvider.Validate(_edmModel, odataUri);

            var queryContext = new OeQueryContext(_edmModel, odataUri) { MetadataLevel = headers.MetadataLevel };

            if (queryContext.ODataUri.Path.LastSegment is OperationSegment)
            {
                using (IAsyncEnumerator<Object> asyncEnumerator = OeOperationHelper.ApplyBoundFunction(queryContext).GetEnumerator())
                    await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, stream, cancellationToken).ConfigureAwait(false);

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
                    using (IAsyncEnumerator<Object> asyncEnumerator = dataAdapter.Execute(dataContext, queryContext).GetEnumerator())
                        await Writers.OeGetWriter.SerializeAsync(queryContext, asyncEnumerator, headers.ContentType, stream, cancellationToken).ConfigureAwait(false);
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
