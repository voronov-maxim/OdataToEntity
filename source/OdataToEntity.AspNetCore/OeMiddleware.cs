﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public class OeMiddleware
    {
        private readonly PathString _apiPath;
        private readonly RequestDelegate _next;

        public OeMiddleware(RequestDelegate next, PathString apiPath, IEdmModel edmModel)
        {
            _next = next;
            _apiPath = apiPath;

            EdmModel = edmModel;
        }

        private static async Task WriteMetadataAsync(IEdmModel edmModel, Stream stream)
        {
            var writerSettings = new ODataMessageWriterSettings();
            writerSettings.EnableMessageStreamDisposal = false;
            IODataResponseMessage message = new Infrastructure.OeInMemoryMessage(stream, null);
            using (var writer = new ODataMessageWriter((IODataResponseMessageAsync)message, writerSettings, edmModel))
                await writer.WriteMetadataDocumentAsync().ConfigureAwait(false);
        }
        private static void GetJsonSchema(IEdmModel edmModel, Stream stream)
        {
            using (var memoryStream = new MemoryStream()) //kestrel allow only async operation
            {
                var schemaGenerator = new ModelBuilder.OeJsonSchemaGenerator(edmModel);
                schemaGenerator.Generate(memoryStream);
                memoryStream.Position = 0;
                memoryStream.CopyToAsync(stream);
            }
        }
        protected virtual Query.OeModelBoundProvider? GetModelBoundProvider(HttpContext httpContext)
        {
            return null;
        }
        private static async Task GetServiceDocumentAsync(IEdmModel edmModel, Uri baseUri, Stream stream)
        {
            var settings = new ODataMessageWriterSettings()
            {
                BaseUri = baseUri,
                Version = ODataVersion.V4,
                ODataUri = new ODataUri() { ServiceRoot = baseUri },
                EnableMessageStreamDisposal = false
            };
            ODataServiceDocument serviceDocument = ODataUtils.GenerateServiceDocument(edmModel);
            IODataResponseMessage responseMessage = new Infrastructure.OeInMemoryMessage(stream, null);
            using (var messageWriter = new ODataMessageWriter(responseMessage, settings))
                await messageWriter.WriteServiceDocumentAsync(serviceDocument);
        }
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.PathBase == _apiPath)
            {
                if (httpContext.Request.Path == "/$metadata")
                    await InvokeMetadataAsync(httpContext).ConfigureAwait(false);
                else if (httpContext.Request.Path == "/$batch")
                    await InvokeBatchAsync(httpContext).ConfigureAwait(false);
                else if (httpContext.Request.Path == "/$json-schema")
                    InvokeJsonSchema(httpContext);
                else if (httpContext.Request.Path == "" || httpContext.Request.Path == "/")
                    await InvokeServiceDocumentAsync(httpContext).ConfigureAwait(false);
                else
                    await InvokeApi(httpContext).ConfigureAwait(false);
            }
            else
                await _next(httpContext).ConfigureAwait(false);
        }
        private async Task InvokeApi(HttpContext httpContext)
        {
            httpContext.Request.Headers.TryGetValue("Prefer", out StringValues preferHeader);
            OeRequestHeaders headers = OeRequestHeaders.Parse(httpContext.Request.Headers["Accept"], preferHeader);

            Uri baseUri = UriHelper.GetBaseUri(httpContext.Request);
            Uri requestUri = UriHelper.GetUri(httpContext.Request);
            if (HttpMethods.IsGet(httpContext.Request.Method))
            {
                var parser = new OeParser(baseUri, EdmModel, GetModelBoundProvider(httpContext), OeParser.ServiceProvider);
                await parser.ExecuteGetAsync(requestUri, new OeHttpRequestHeaders(headers, httpContext.Response),
                    httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
            }
            else if (HttpMethods.IsPost(httpContext.Request.Method) ||
                HttpMethods.IsPut(httpContext.Request.Method) ||
                HttpMethods.IsPatch(httpContext.Request.Method) ||
                HttpMethods.IsDelete(httpContext.Request.Method))
            {
                ODataUri odataUri = OeParser.ParseUri(EdmModel, baseUri, requestUri, OeParser.ServiceProvider);
                if (odataUri.Path.LastSegment is OperationImportSegment)
                {
                    var parser = new OeParser(baseUri, EdmModel, GetModelBoundProvider(httpContext), OeParser.ServiceProvider);
                    await parser.ExecuteOperationAsync(odataUri, new OeHttpRequestHeaders(headers, httpContext.Response),
                        httpContext.Request.Body, httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
                }
                else
                {
                    httpContext.Response.ContentType = httpContext.Request.ContentType;
                    var batchParser = new OeBatchParser(baseUri, EdmModel, OeParser.ServiceProvider);
                    await batchParser.ExecuteOperationAsync(requestUri, httpContext.Request.Body, httpContext.Response.Body,
                        httpContext.Request.ContentType, httpContext.Request.Method, httpContext.RequestAborted).ConfigureAwait(false);
                }
            }
        }
        private async Task InvokeBatchAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = httpContext.Request.ContentType;
            var parser = new OeParser(UriHelper.GetBaseUri(httpContext.Request), EdmModel);
            await parser.ExecuteBatchAsync(httpContext.Request.Body, httpContext.Response.Body,
                httpContext.Request.ContentType, httpContext.RequestAborted).ConfigureAwait(false);
        }
        private void InvokeJsonSchema(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "application/schema+json";
            GetJsonSchema(EdmModel, httpContext.Response.Body);
        }
        private Task InvokeMetadataAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "application/xml";
            return WriteMetadataAsync(EdmModel, httpContext.Response.Body);
        }
        private Task InvokeServiceDocumentAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "application/schema+json";
            return GetServiceDocumentAsync(EdmModel, UriHelper.GetBaseUri(httpContext.Request), httpContext.Response.Body);
        }

        protected IEdmModel EdmModel { get; }
    }
}
