using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    [OeFilter]
    public abstract class OeControllerBase : ControllerBase, IDisposable
    {
        private readonly OeDataAdapter _dataAdapter;
        private Object _dataContext;
        private readonly IEdmModel _edmModel;
        private OeQueryContext _queryContext;

        protected OeControllerBase(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dataContext != null)
                    _dataAdapter.CloseDataContext(_dataContext);
            }
        }
        private OeAsyncEnumerator Execute(ODataUri odataUri, Stream requestStream, OeRequestHeaders headers, CancellationToken cancellationToken)
        {
            Object dataContext = _dataAdapter.CreateDataContext();

            var parser = new OePostParser(_dataAdapter, _edmModel);
            OeAsyncEnumerator asyncEnumerator = parser.GetAsyncEnumerator(odataUri, requestStream, headers, dataContext, out Type returnClrType);

            if (returnClrType != null && !(returnClrType.IsPrimitive || returnClrType == typeof(String)))
                _queryContext = parser.CreateQueryContext(odataUri, headers.MetadataLevel, returnClrType);

            return asyncEnumerator;
        }
        private OeAsyncEnumerator Execute(OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            _dataContext = _dataAdapter.CreateDataContext();

            if (_queryContext.IsCountSegment)
            {
                headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                int count = _dataAdapter.ExecuteScalar<int>(_dataContext, _queryContext);
                return new OeScalarAsyncEnumeratorAdapter(Task.FromResult((Object)count), cancellationToken);
            }

            return _dataAdapter.ExecuteEnumerator(_dataContext, _queryContext, cancellationToken);
        }
        protected async Task Get(HttpContext httpContext, Stream responseStream, bool navigationNextLink = false, int? maxPageSize = null)
        {
            var requestHeaders = (HttpRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, httpContext.Response, navigationNextLink, maxPageSize);

            var parser = new OeParser(UriHelper.GetBaseUri(httpContext.Request), _dataAdapter, _edmModel);
            await parser.ExecuteGetAsync(UriHelper.GetUri(httpContext.Request), headers, responseStream, httpContext.RequestAborted);
        }
        protected OeAsyncEnumerator GetAsyncEnumerator(HttpContext httpContext, Stream responseStream, bool navigationNextLink = false, int? maxPageSize = null)
        {
            var odataParser = new ODataUriParser(_edmModel, UriHelper.GetBaseUri(httpContext.Request), UriHelper.GetUri(httpContext.Request));
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();

            var requestHeaders = (HttpRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, httpContext.Response, navigationNextLink, maxPageSize);

            if (odataUri.Path.LastSegment is OperationImportSegment)
                return Execute(odataUri, httpContext.Request.Body, headers, httpContext.RequestAborted);

            var getParser = new OeGetParser(_dataAdapter, _edmModel);
            _queryContext = getParser.CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
            return Execute(headers, responseStream, httpContext.RequestAborted);
        }
        private static OeRequestHeaders GetRequestHeaders(HttpRequestHeaders requestHeaders, HttpResponse httpResponse, bool navigationNextLink, int? maxPageSize)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader).SetNavigationNextLink(navigationNextLink);
            if (maxPageSize != null)
                headers = headers.SetMaxPageSize(maxPageSize.Value);

            return new OeHttpRequestHeaders(headers, httpResponse);
        }
        protected IActionResult OData(OeAsyncEnumerator asyncEnumerator)
        {
            if (asyncEnumerator is OeScalarAsyncEnumeratorAdapter)
                return ODataScalar(asyncEnumerator).GetAwaiter().GetResult();

            Type clrType = _edmModel.GetClrType(_queryContext.EntryFactory.EntityType);
            Func<OeAsyncEnumerator, IActionResult> odataFunc = OData<Object>;
            return (IActionResult)odataFunc.Method.GetGenericMethodDefinition().MakeGenericMethod(clrType).Invoke(this, new Object[] { asyncEnumerator });
        }
        protected ODataResult<T> OData<T>(OeAsyncEnumerator asyncEnumerator)
        {
            var entityAsyncEnumerator = new OeEntityAsyncEnumerator<T>(asyncEnumerator, _queryContext.EntryFactory, _queryContext);
            HttpContext.Response.RegisterForDispose(entityAsyncEnumerator);
            return new ODataResult<T>(_edmModel, _queryContext.ODataUri, entityAsyncEnumerator)
            {
                Count = asyncEnumerator.Count,
                PageSize = _queryContext.PageSize
            };
        }
        protected async Task<IActionResult> ODataScalar(OeAsyncEnumerator asyncEnumerator)
        {
            if (await asyncEnumerator.MoveNextAsync() && asyncEnumerator.Current != null)
                return new ContentResult()
                {
                    Content = asyncEnumerator.Current.ToString(),
                    ContentType = OeRequestHeaders.TextDefault.ContentType
                };

            base.HttpContext.Response.ContentType = null;
            return new EmptyResult();
        }
    }
}
