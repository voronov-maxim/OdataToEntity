using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;
using OdataToEntity.Query;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OdataToEntity
{
    public static class OdataToEntityHelper
    {
        private readonly static ConcurrentDictionary<int, OeModelBoundProvider> _cache = new ConcurrentDictionary<int, OeModelBoundProvider>();

        public static OeModelBoundProvider CreateModelBoundProvider(this HttpContext httpContext)
        {
            var edmModel = (IEdmModel)httpContext.RequestServices.GetService(typeof(IEdmModel));
            return httpContext.CreateModelBoundProvider(edmModel);
        }
        public static OeModelBoundProvider CreateModelBoundProvider(this HttpContext httpContext, IEdmModel edmModel)
        {
            var requestHeaders = (HttpRequestHeaders)httpContext.Request.Headers;
            int maxPageSize = GetMaxPageSize(requestHeaders);
            if (maxPageSize <= 0)
                return null;

            if (!_cache.TryGetValue(maxPageSize, out OeModelBoundProvider modelBoundProvider))
            {
                modelBoundProvider = CreateModelBoundProvider(edmModel, maxPageSize);
                _cache.TryAdd(maxPageSize, modelBoundProvider);
            }
            return modelBoundProvider;
        }
        public static OeModelBoundProvider CreateModelBoundProvider(IEdmModel edmModel, int pageSize)
        {
            var pageNextLinkModelBoundBuilder = new Test.PageNextLinkModelBoundBuilder(edmModel, false);
            return pageNextLinkModelBoundBuilder.BuildProvider(pageSize, false);
        }
        public static int GetMaxPageSize(HttpRequestHeaders requestHeaders)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader);
            return headers.MaxPageSize;
        }
    }
}