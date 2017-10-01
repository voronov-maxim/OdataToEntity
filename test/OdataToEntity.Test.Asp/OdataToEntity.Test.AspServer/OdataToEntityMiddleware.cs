using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using OdataToEntity.Db;
using OdataToEntity.EfCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace OdataToEntity.AspServer
{
    public sealed class OdataToEntityMiddleware
    {
        private readonly PathString _apiPath;
        private readonly Uri _baseUri;

        private OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OdataToEntityMiddleware(RequestDelegate next, PathString apiPath, OeDataAdapter dataAdapter)
        {
            _apiPath = apiPath;
            _baseUri = new Uri("http://dummy" + apiPath);

            _dataAdapter = dataAdapter;
            _edmModel = _dataAdapter.BuildEdmModelFromEfCoreModel();
        }

        private static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            IEnumerable<EdmError> errors;
            using (XmlWriter xmlWriter = XmlWriter.Create(stream))
                if (CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out errors))
                    return true;

            return false;
        }
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path == "/$metadata")
                InvokeMetadata(httpContext);
            else if (httpContext.Request.Path == "/$batch")
                await InvokeBatch(httpContext);
            else
                await Invoke(httpContext, httpContext.Request.Path);
        }
        private async Task Invoke(HttpContext httpContext, PathString remaining)
        {
            var uri = new Uri(_baseUri.OriginalString + remaining + httpContext.Request.QueryString);
            var requestHeaders = (FrameRequestHeaders)httpContext.Request.Headers;
            OeRequestHeaders headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept);
            var parser = new OeParser(_baseUri, _dataAdapter, _edmModel);
            await parser.ExecuteGetAsync(uri, new OeHttpRequestHeaders(headers, httpContext.Response), httpContext.Response.Body, CancellationToken.None);
        }
        private async Task InvokeBatch(HttpContext httpContext)
        {
            httpContext.Response.ContentType = httpContext.Request.ContentType;
            var parser = new OeParser(_baseUri, _dataAdapter, _edmModel);
            await parser.ExecuteBatchAsync(httpContext.Request.Body, httpContext.Response.Body,
                CancellationToken.None, httpContext.Request.ContentType);
        }
        private void InvokeMetadata(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "application/xml";
            GetCsdlSchema(_edmModel, httpContext.Response.Body);
        }
    }
}
