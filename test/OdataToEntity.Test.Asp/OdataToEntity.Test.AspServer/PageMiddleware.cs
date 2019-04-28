using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using OdataToEntity.Query;

namespace OdataToEntity.AspServer
{
    public sealed class PageMiddleware : AspNetCore.OeMiddleware
    {
        public PageMiddleware(RequestDelegate next, PathString apiPath, IEdmModel edmModel)
            : base(next, apiPath, edmModel)
        {
        }

        protected override OeModelBoundProvider GetModelBoundProvider(HttpContext httpContext)
        {
            return httpContext.CreateModelBoundProvider(base.EdmModel);
        }
    }
}