using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.OData.Edm;
using OdataToEntity.AspServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public abstract class OeBaseController : Controller
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;
        private static readonly Uri _rootUri = new Uri("http://dummy");

        protected OeBaseController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        protected async Task Get(HttpContext httpContext, Stream responseStream)
        {
            var requestHeaders = (FrameRequestHeaders)httpContext.Request.Headers;
            httpContext.Response.ContentType = requestHeaders.HeaderAccept;

            var apiSegment = httpContext.Request.Path.Value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            var parser = new OeParser(apiSegment.Length == 1 ? _rootUri : new Uri(_rootUri, apiSegment[0]), _dataAdapter, _edmModel);

            var uri = new Uri(_rootUri.OriginalString + httpContext.Request.Path + httpContext.Request.QueryString);
            OeRequestHeaders headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept);
            await parser.ExecuteGetAsync(uri, new OeHttpRequestHeaders(headers, httpContext.Response), responseStream, CancellationToken.None);
        }
    }
}
