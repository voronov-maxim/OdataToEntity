using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class OeAspQueryParser : IDisposable
    {
        private Db.OeDataAdapter? _dataAdapter;
        private Object? _dataContext;
        private readonly IEdmModel _edmModel;
        private readonly HttpContext _httpContext;
        private readonly Query.OeModelBoundProvider? _modelBoundProvider;
        private OeQueryContext? _queryContext;
        private ODataUri? _odataUri;

        public OeAspQueryParser(HttpContext httpContext, Query.OeModelBoundProvider? modelBoundProvider = null)
        {
            _httpContext = httpContext;
            _edmModel = _httpContext.GetEdmModel();
            _modelBoundProvider = modelBoundProvider;
        }

        public void Dispose()
        {
            if (_dataAdapter != null && _dataContext != null)
                _dataAdapter.CloseDataContext(_dataContext);
        }
        private IAsyncEnumerable<Object?> ExecuteGet(IEdmModel refModel, ODataUri odataUri, OeRequestHeaders headers, IQueryable? source)
        {
            if (_modelBoundProvider != null)
                _modelBoundProvider.Validate(_edmModel, odataUri);

            _queryContext = new OeQueryContext(refModel, odataUri) { MetadataLevel = headers.MetadataLevel };

            if (odataUri.Path.LastSegment is OperationSegment)
                return OeOperationHelper.ApplyBoundFunction(_queryContext);

            if (odataUri.Path.LastSegment is CountSegment)
            {
                headers.ResponseContentType = OeRequestHeaders.TextDefault.MimeType;
                int count = _dataAdapter!.ExecuteScalar<int>(_dataContext!, _queryContext);
                return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(Task.FromResult((Object)count));
            }

            if (source != null)
                _queryContext.QueryableSource = e => e == _queryContext.EntryFactory!.EntitySet ? source : null;

            return _dataAdapter!.Execute(_dataContext!, _queryContext);
        }
        private IAsyncEnumerable<Object> ExecutePost(IEdmModel refModel, ODataUri odataUri, OeRequestHeaders headers, Stream requestStream, CancellationToken cancellationToken)
        {
            _odataUri = odataUri;

            var parser = new OePostParser(refModel, null);
            IAsyncEnumerable<Object> asyncEnumerable = parser.GetAsyncEnumerable(odataUri, requestStream, headers, _dataContext!, cancellationToken, out bool isScalar);
            if (!isScalar)
            {
                var importSegment = (OperationImportSegment)odataUri.Path.FirstSegment;
                IEdmEntitySet? entitySet = OeOperationHelper.GetEntitySet(importSegment.OperationImports.Single());
                if (entitySet != null)
                    _queryContext = parser.CreateQueryContext(odataUri, entitySet, headers.MetadataLevel);
            }

            return asyncEnumerable;
        }
        public IAsyncEnumerable<T> ExecuteReader<T>(IQueryable? source = null, CancellationToken cancellationToken = default)
        {
            IAsyncEnumerable<Object?> asyncEnumerable = GetAsyncEnumerator(source);
            if (OeExpressionHelper.IsPrimitiveType(typeof(T)) || _queryContext == null || (_queryContext.EntryFactory != null && !_queryContext.EntryFactory.IsTuple))
                return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable<T>(asyncEnumerable, cancellationToken);

            return new Db.OeEntityAsyncEnumeratorAdapter<T>(asyncEnumerable.GetAsyncEnumerator(), _queryContext);
        }
        public async Task<T?> ExecuteScalar<T>(IQueryable? source = null) where T : struct
        {
            IAsyncEnumerator<Object?>? asyncEnumerator = null;
            try
            {
                asyncEnumerator = GetAsyncEnumerator(source).GetAsyncEnumerator();
                if (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false) && asyncEnumerator.Current != null)
                    return (T)asyncEnumerator.Current;
            }
            finally
            {
                if (asyncEnumerator != null)
                    await asyncEnumerator.DisposeAsync();
            }

            _httpContext.Response.ContentType = null!;
            return null;
        }
        public static async Task Get(HttpContext httpContext, Query.OeModelBoundProvider? modelBoundProvider = null)
        {
            var parser = new OeParser(UriHelper.GetBaseUri(httpContext.Request), httpContext.GetEdmModel(), modelBoundProvider, null);
            OeRequestHeaders headers = GetRequestHeaders(httpContext.Request.Headers, httpContext.Response);
            await parser.ExecuteGetAsync(UriHelper.GetUri(httpContext.Request), headers, httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }
        private IAsyncEnumerable<Object?> GetAsyncEnumerator(IQueryable? source = null)
        {
            _httpContext.Response.RegisterForDispose(this);

            ODataUri odataUri = OeParser.ParseUri(_edmModel, UriHelper.GetBaseUri(_httpContext.Request), UriHelper.GetUri(_httpContext.Request));
            IEdmModel refModel = _edmModel.GetEdmModel(odataUri.Path);
            _dataAdapter = refModel.GetDataAdapter(refModel.EntityContainer);
            if (_dataContext == null)
                _dataContext = _dataAdapter.CreateDataContext();

            OeRequestHeaders headers = GetRequestHeaders(_httpContext.Request.Headers, _httpContext.Response);
            if (odataUri.Path.LastSegment is OperationImportSegment)
                return ExecutePost(refModel, odataUri, headers, _httpContext.Request.Body, _httpContext.RequestAborted);
            else
                return ExecuteGet(refModel, odataUri, headers, source);
        }
        public TDataContext? GetDbContext<TDataContext>() where TDataContext : class
        {
            if (_dataContext == null)
            {
                Db.OeDataAdapter dataAdapter = _edmModel.GetDataAdapter(typeof(TDataContext));
                if (dataAdapter == null)
                    return null;

                _dataContext = dataAdapter.CreateDataContext();
            }
            return (TDataContext)_dataContext;
        }
        private static OeRequestHeaders GetRequestHeaders(IHeaderDictionary requestHeaders, HttpResponse httpResponse)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders["Accept"], preferHeader);
            return new OeHttpRequestHeaders(headers, httpResponse);
        }
        public ODataResult<T> OData<T>(IAsyncEnumerable<T> asyncEnumerable)
        {
            IAsyncEnumerator<T> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();
            _httpContext.Response.OnCompleted(() => asyncEnumerator.DisposeAsync().AsTask());

            if (_odataUri != null && (OeExpressionHelper.IsPrimitiveType(typeof(T)) || _odataUri.Path.LastSegment is CountSegment))
                return new ODataPrimitiveResult<T>(_edmModel, _odataUri, asyncEnumerator);

            if (_queryContext == null)
                throw new InvalidOperationException("Must invoke OeAspQueryParser.ExecuteReader");

            return new ODataResult<T>(_queryContext, asyncEnumerator);
        }
        public ODataResult<T> OData<T>(IEnumerable<T> enumerable)
        {
            return OData(enumerable.ToAsyncEnumerable());
        }
        public IActionResult OData<T>(T? value) where T : struct
        {
            if (value == null)
            {
                _httpContext.Response.ContentType = null!;
                return new EmptyResult();
            }

            return new ContentResult()
            {
                Content = value.ToString(),
                ContentType = OeRequestHeaders.TextDefault.ContentType
            };

        }
    }
}
