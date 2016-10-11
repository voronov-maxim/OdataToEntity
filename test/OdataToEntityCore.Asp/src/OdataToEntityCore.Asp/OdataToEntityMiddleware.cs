using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using OdataToEntity;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntityCore.Asp
{
    public sealed class OdataToEntityMiddleware
    {
        private readonly Uri _baseUri;
        private readonly PathString _endpointPath;
        private readonly RequestDelegate _next;

        private OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OdataToEntityMiddleware(RequestDelegate next, Type dataAdapterType, String endpointPath, IServiceProvider serviceProvider)
        {
            _next = next;
            _endpointPath = new PathString("/" + endpointPath);
            _baseUri = new Uri("http://dummy" + _endpointPath);

            _dataAdapter = (OeDataAdapter)serviceProvider.GetService(dataAdapterType);
            _edmModel = new OeEdmModelBuilder(_dataAdapter.EntitySetMetaAdapters.ToDictionary()).BuildEdmModel();
        }

        public async Task Invoke(HttpContext httpContext)
        {
            PathString remaining;
            if (httpContext.Request.Path.StartsWithSegments(_endpointPath, StringComparison.Ordinal, out remaining))
            {
                if (remaining.StartsWithSegments("/$metadata"))
                    InvokeMetadata(httpContext);
                else if (remaining.StartsWithSegments("/$batch"))
                    await InvokeBatch(httpContext);
                else
                    await Invoke(httpContext, remaining);
            }
            else
                await _next.Invoke(httpContext);
        }
        private async Task Invoke(HttpContext httpContext, PathString remaining)
        {
            var accept = "application/json;odata.metadata=minimal";
            httpContext.Response.ContentType = accept;

            var uri = new Uri(_baseUri.OriginalString + remaining + httpContext.Request.QueryString);
            OeRequestHeaders headers = OeRequestHeaders.Parse(accept);
            var parser = new OeParser(_baseUri, _dataAdapter, _edmModel);
            await parser.ExecuteQueryAsync(uri, headers, httpContext.Response.Body, CancellationToken.None);
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
            OeModelBuilderHelper.GetCsdlSchema(_edmModel, httpContext.Response.Body);
        }
    }
}
