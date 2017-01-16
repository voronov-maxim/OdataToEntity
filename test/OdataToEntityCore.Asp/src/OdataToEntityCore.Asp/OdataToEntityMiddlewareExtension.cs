using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http;
using OdataToEntity.Db;
using System;

namespace OdataToEntityCore.Asp
{
    public static class OdataToEntityMiddlewareExtension
    {
        public static IApplicationBuilder UseOdataToEntityMiddleware(this IApplicationBuilder app, PathString pathMatch, OeDataAdapter dataAdapater)
        {
            if (app == null)
                throw new ArgumentNullException("app");
            if (pathMatch.HasValue && pathMatch.Value.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("The path must not end with a '/'", "pathMatch");

            IApplicationBuilder applicationBuilder = app.New();
            applicationBuilder.UseMiddleware<OdataToEntityMiddleware>(pathMatch, dataAdapater);
            RequestDelegate branch = applicationBuilder.Build();
            MapOptions options = new MapOptions
            {
                Branch = branch,
                PathMatch = pathMatch
            };
            return app.Use((RequestDelegate next) => new RequestDelegate(new MapMiddleware(next, options).Invoke));
        }
    }
}
