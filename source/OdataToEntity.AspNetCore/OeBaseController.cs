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
    public abstract class OeBaseController : Controller
    {
        private readonly OeDataAdapter _dataAdapter;
        private Object _dataContext;
        private readonly IEdmModel _edmModel;
        private OeQueryContext _queryContext;

        protected OeBaseController(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dataContext != null)
                    _dataAdapter.CloseDataContext(_dataContext);
            }
            base.Dispose(disposing);
        }
        protected async Task Get(HttpContext httpContext, Stream responseStream, bool navigationNextLink = false, int? maxPageSize = null)
        {
            var requestHeaders = (FrameRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, httpContext.Response, navigationNextLink, maxPageSize);

            var parser = new OeParser(GetBaseUri(httpContext), _dataAdapter, _edmModel);
            await parser.ExecuteGetAsync(GetUri(httpContext), headers, responseStream, httpContext.RequestAborted);
        }
        protected async Task<OeAsyncEnumerator> GetAsyncEnumerator(HttpContext httpContext, Stream responseStream, bool navigationNextLink = false, int? maxPageSize = null)
        {
            var odataParser = new ODataUriParser(_edmModel, GetBaseUri(httpContext), GetUri(httpContext));
            odataParser.Resolver.EnableCaseInsensitive = true;
            ODataUri odataUri = odataParser.ParseUri();

            var requestHeaders = (FrameRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = GetRequestHeaders(requestHeaders, httpContext.Response, navigationNextLink, maxPageSize);

            if (odataUri.Path.LastSegment is OperationImportSegment)
            {
                var parser = new OeParser(GetBaseUri(httpContext), _dataAdapter, _edmModel);
                await parser.ExecuteOperationAsync(odataUri, headers, null, responseStream, httpContext.RequestAborted);
                return null;
            }
            else
            {
                var getParser = new OeGetParser(_dataAdapter, _edmModel);
                _queryContext = getParser.CreateQueryContext(odataUri, headers.MaxPageSize, headers.NavigationNextLink, headers.MetadataLevel);
                return Execute(headers, responseStream, httpContext.RequestAborted);
            }
        }
        protected OeAsyncEnumerator Execute(OeRequestHeaders headers, Stream responseStream, CancellationToken cancellationToken)
        {
            _dataContext = _dataAdapter.CreateDataContext();

            if (_queryContext.IsCountSegment)
            {
                headers.ResponseContentType = OeRequestHeaders.TextDefault.ContentType;
                int count = _dataAdapter.ExecuteScalar<int>(_dataContext, _queryContext);
                return new OeScalarAsyncEnumeratorAdapter(Task.FromResult((Object)count), cancellationToken);
            }
            else
                return _dataAdapter.ExecuteEnumerator(_dataContext, _queryContext, cancellationToken);
        }
        private static Uri GetBaseUri(HttpContext httpContext)
        {
            var rootUri = new Uri(httpContext.Request.Scheme + "://" + httpContext.Request.Host);
            String[] apiSegment = httpContext.Request.Path.Value.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return apiSegment.Length == 1 ? rootUri : new Uri(rootUri, apiSegment[0]);
        }
        private static OeRequestHeaders GetRequestHeaders(FrameRequestHeaders requestHeaders, HttpResponse httpResponse, bool navigationNextLink, int? maxPageSize)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader).SetNavigationNextLink(navigationNextLink);
            if (maxPageSize != null)
                headers = headers.SetMaxPageSize(maxPageSize.Value);

            return new OeHttpRequestHeaders(headers, httpResponse);
        }
        private static Uri GetUri(HttpContext httpContext)
        {
            var rootUri = new Uri(httpContext.Request.Scheme + "://" + httpContext.Request.Host);
            return new Uri(rootUri.OriginalString + httpContext.Request.Path + httpContext.Request.QueryString);
        }
        protected ActionResult OData(OeAsyncEnumerator asyncEnumerator)
        {
            Type clrType = _edmModel.GetClrType(_queryContext.EntryFactory.EntityType);
            Func<OeAsyncEnumerator, ActionResult> odataFunc = OData<Object>;
            return (ActionResult)odataFunc.Method.GetGenericMethodDefinition().MakeGenericMethod(clrType).Invoke(this, new Object[] { asyncEnumerator });
        }
        protected ActionResult OData<T>(OeAsyncEnumerator asyncEnumerator)
        {
            var entityAsyncEnumerator = new OeEntityAsyncEnumerator<T>(_queryContext.EntryFactory, asyncEnumerator);
            return new ODataResult<T>(_edmModel, _queryContext.ODataUri, entityAsyncEnumerator)
            {
                Count = asyncEnumerator.Count,
                PageSize = _queryContext.PageSize
            };
        }
    }
}
