using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;
using OdataToEntity.AspServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public abstract class OeBaseController : Controller
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        protected OeBaseController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        protected async Task Get(HttpContext httpContext, Stream responseStream, bool navigationNextLink = false, int pageSize = 0)
        {
            var rootUri = new Uri(httpContext.Request.Scheme + "://" + httpContext.Request.Host);
            var requestHeaders = (FrameRequestHeaders)httpContext.Request.Headers;
            httpContext.Response.ContentType = requestHeaders.HeaderAccept;

            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            OeRequestHeaders headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader).SetNavigationNextLink(navigationNextLink);

            var uri = new Uri(rootUri.OriginalString + httpContext.Request.Path + httpContext.Request.QueryString);
            String[] apiSegment = httpContext.Request.Path.Value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            var parser = new OeParser(apiSegment.Length == 1 ? rootUri : new Uri(rootUri, apiSegment[0]), _dataAdapter, _edmModel);
            await parser.ExecuteGetAsync(uri, new OeHttpRequestHeaders(headers, httpContext.Response), responseStream, CancellationToken.None);
        }
    }
}
