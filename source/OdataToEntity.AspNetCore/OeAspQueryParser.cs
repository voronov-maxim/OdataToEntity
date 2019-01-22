using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Db;
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
        private int? _count;
        private OeDataAdapter _dataAdapter;
        private Object _dataContext;
        private readonly IEdmModel _edmModel;
        private readonly HttpContext _httpContext;
        private OeQueryContext _queryContext;
        private ODataUri _odataUri;

        public OeAspQueryParser(HttpContext httpContext)
        {
            _httpContext = httpContext;
            _edmModel = (IEdmModel)_httpContext.RequestServices.GetService(typeof(IEdmModel));
        }

        public void Dispose()
        {
            if (_dataContext != null)
                _dataAdapter.CloseDataContext(_dataContext);
        }
        private OeAsyncEnumerator ExecuteGet(IEdmModel refModel, ODataUri odataUri, OeRequestHeaders headers, CancellationToken cancellationToken, IQueryable source)
        {
            var parser = new OeGetParser(refModel);
            _queryContext = parser.CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
            if (source != null)
                _queryContext.QueryableSource = e => e.Name == _queryContext.EntryFactory.EntitySet.Name ? source : null;

            if (_queryContext.IsCountSegment)
            {
                headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                int count = _dataAdapter.ExecuteScalar<int>(_dataContext, _queryContext);
                return new OeScalarAsyncEnumeratorAdapter(Task.FromResult((Object)count), cancellationToken);
            }

            return _dataAdapter.ExecuteEnumerator(_dataContext, _queryContext, cancellationToken);
        }
        private OeAsyncEnumerator ExecutePost(IEdmModel refModel, ODataUri odataUri, OeRequestHeaders headers, CancellationToken cancellationToken, Stream requestStream)
        {
            _odataUri = odataUri;

            var parser = new OePostParser(refModel);
            OeAsyncEnumerator asyncEnumerator = parser.GetAsyncEnumerator(odataUri, requestStream, headers, _dataContext, out bool isScalar);
            if (!isScalar)
                _queryContext = parser.CreateQueryContext(odataUri, headers.MetadataLevel);

            return asyncEnumerator;
        }
        public IAsyncEnumerable<T> ExecuteReader<T>(IQueryable source = null, bool navigationNextLink = false, int? maxPageSize = null)
        {
            OeAsyncEnumerator asyncEnumerator = GetAsyncEnumerator(source, navigationNextLink, maxPageSize);
            _count = asyncEnumerator.Count;
            if (asyncEnumerator is OeAsyncEnumeratorAdapter || OeExpressionHelper.IsPrimitiveType(typeof(T)))
                return new OeAsyncEnumeratorAdapter<T>(asyncEnumerator);

            return new OeEntityAsyncEnumeratorAdapter<T>(asyncEnumerator, _queryContext);
        }
        public async Task<T?> ExecuteScalar<T>(IQueryable source = null) where T : struct
        {
            OeAsyncEnumerator asyncEnumerator = GetAsyncEnumerator(source);
            if (await asyncEnumerator.MoveNextAsync() && asyncEnumerator.Current != null)
                return (T)asyncEnumerator.Current;

            _httpContext.Response.ContentType = null;
            return null;
        }
        public static async Task Get(HttpContext httpContext, bool navigationNextLink = false, int? maxPageSize = null)
        {
            var requestHeaders = (HttpRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, httpContext.Response, navigationNextLink, maxPageSize);

            var edmModel = (IEdmModel)httpContext.RequestServices.GetService(typeof(IEdmModel));
            var parser = new OeParser(UriHelper.GetBaseUri(httpContext.Request), edmModel);
            await parser.ExecuteGetAsync(UriHelper.GetUri(httpContext.Request), headers, httpContext.Response.Body, httpContext.RequestAborted);
        }
        private OeAsyncEnumerator GetAsyncEnumerator(IQueryable source = null, bool navigationNextLink = false, int? maxPageSize = null)
        {
            _httpContext.Response.RegisterForDispose(this);

            ODataUri odataUri = OeParser.ParseUri(_edmModel, UriHelper.GetBaseUri(_httpContext.Request), UriHelper.GetUri(_httpContext.Request));
            IEdmModel refModel = _edmModel.GetEdmModel(odataUri.Path);
            _dataAdapter = refModel.GetDataAdapter(refModel.EntityContainer);
            if (_dataContext == null)
                _dataContext = _dataAdapter.CreateDataContext();

            var requestHeaders = (HttpRequestHeaders)_httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, _httpContext.Response, navigationNextLink, maxPageSize);

            if (odataUri.Path.LastSegment is OperationImportSegment)
                return ExecutePost(refModel, odataUri, headers, _httpContext.RequestAborted, _httpContext.Request.Body);
            else
                return ExecuteGet(refModel, odataUri, headers, _httpContext.RequestAborted, source);
        }
        public TDataContext GetDbContext<TDataContext>() where TDataContext : class
        {
            if (_dataContext == null)
            {
                OeDataAdapter dataAdapter = _edmModel.GetDataAdapter(typeof(TDataContext));
                if (dataAdapter == null)
                    return null;

                _dataContext = dataAdapter.CreateDataContext();
            }
            return (TDataContext)_dataContext;
        }
        private static OeRequestHeaders GetRequestHeaders(HttpRequestHeaders requestHeaders, HttpResponse httpResponse, bool navigationNextLink, int? maxPageSize)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader).SetNavigationNextLink(navigationNextLink);
            if (maxPageSize != null)
                headers = headers.SetMaxPageSize(maxPageSize.Value);

            return new OeHttpRequestHeaders(headers, httpResponse);
        }
        public ODataResult<T> OData<T>(IAsyncEnumerable<T> asyncEnumerable, int? count = null)
        {
            IAsyncEnumerator<T> asyncEnumerator = asyncEnumerable.GetEnumerator();
            if (count == null)
                count = (asyncEnumerator as OeAsyncEnumerator)?.Count;

            _httpContext.Response.RegisterForDispose(asyncEnumerator);
            if (OeExpressionHelper.IsPrimitiveType(typeof(T)))
                return new ODataPrimitiveResult<T>(_edmModel, _odataUri, asyncEnumerator) { Count = count };

            return new ODataResult<T>(_edmModel, _queryContext.ODataUri, _queryContext.EntryFactory.EntitySet, asyncEnumerator)
            {
                Count = count,
                PageSize = _queryContext.PageSize
            };
        }
        public ODataResult<T> OData<T>(IEnumerable<T> enumerable)
        {
            return OData(enumerable.ToAsyncEnumerable(), _count);
        }
        public IActionResult OData<T>(T? value) where T : struct
        {
            if (value == null)
            {
                _httpContext.Response.ContentType = null;
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
