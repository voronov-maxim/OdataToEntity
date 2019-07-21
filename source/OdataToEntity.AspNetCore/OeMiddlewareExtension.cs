using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.AspNetCore
{
    public static class OeMiddlewareExtension
    {
        public static IServiceCollection AddOdataToEntityMvc(this IServiceCollection services, IEdmModel edmModel)
        {
            services.AddSingleton(edmModel).AddSingleton<OeRouter>().AddHttpContextAccessor();
            services.AddMvcCore(o => o.Conventions.Add(new OeBatchFilterConvention()));
            return services;
        }
        public static IRouteBuilder AddOdataToEntityRoute(this IRouteBuilder routeBuilder)
        {
            var router = routeBuilder.ServiceProvider.GetService<OeRouter>() ??
                throw new InvalidOperationException("Use IServiceCollection AddOdataToEntity extension method");
            routeBuilder.Routes.Add(router);
            return routeBuilder;
        }
        public static IApplicationBuilder UseOdataToEntityMiddleware(this IApplicationBuilder app, PathString apiPath, IEdmModel edmModel)
        {
            return app.UseOdataToEntityMiddleware<OeMiddleware>(apiPath, edmModel);
        }
        public static IApplicationBuilder UseOdataToEntityMiddleware<TMiddleware>(this IApplicationBuilder app, PathString apiPath, IEdmModel edmModel)
            where TMiddleware : OeMiddleware
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (apiPath.HasValue && apiPath.Value.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("The path must not end with a '/'", nameof(apiPath));

            IApplicationBuilder applicationBuilder = app.New();
            applicationBuilder.UseMiddleware<TMiddleware>(apiPath, edmModel);
            RequestDelegate branch = applicationBuilder.Build();
            MapOptions options = new MapOptions
            {
                Branch = branch,
                PathMatch = apiPath
            };
            return app.Use((RequestDelegate next) => new RequestDelegate(new MapMiddleware(next, options).Invoke));
        }
    }
}
