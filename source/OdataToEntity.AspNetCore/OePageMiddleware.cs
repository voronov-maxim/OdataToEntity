using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using OdataToEntity.Query;

namespace OdataToEntity.AspNetCore
{
    public sealed class OePageMiddleware : OeMiddleware
    {
        public OePageMiddleware(RequestDelegate next, PathString apiPath, IEdmModel edmModel)
            : base(next, apiPath, edmModel)
        {
        }

        protected override OeModelBoundProvider? GetModelBoundProvider(HttpContext httpContext)
        {
            return httpContext.CreateModelBoundProvider(base.EdmModel);
        }
    }
}