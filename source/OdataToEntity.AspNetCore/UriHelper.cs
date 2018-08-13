using Microsoft.AspNetCore.Http;
using System;

namespace OdataToEntity.AspNetCore
{
    internal static class UriHelper
    {
        public static Uri GetBaseUri(HttpRequest request)
        {
            var uriBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port.GetValueOrDefault());
            if (request.PathBase.HasValue)
                uriBuilder.Path = request.PathBase;
            else
            {
                if (request.Path.Value.Length > 1)
                {
                    int i = request.Path.Value.IndexOf('/', 1);
                    if (i > 0)
                        uriBuilder.Path = request.Path.Value.Substring(1, i - 1);
                }
            }
            return uriBuilder.Uri;
        }
        public static Uri GetBaseUri(HttpRequest request, String controllerName)
        {
            var uriBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port.GetValueOrDefault());
            if (request.PathBase.HasValue)
                uriBuilder.Path = request.PathBase;
            else
            {
                if (request.Path.Value.Length > 1)
                {
                    int i = request.Path.Value.IndexOf("/" + controllerName);
                    if (i > 0)
                        uriBuilder.Path = request.Path.Value.Substring(1, i - 1);
                    else if (i < 0)
                    {
                        i = request.Path.Value.IndexOf('/', 1);
                        if (i > 0)
                            uriBuilder.Path = request.Path.Value.Substring(1, i - 1);
                    }

                }
            }
            return uriBuilder.Uri;
        }
        public static Uri GetUri(HttpRequest request)
        {
            var path = request.PathBase.Add(request.Path);
            var uriBuilder = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port.GetValueOrDefault(), path, request.QueryString.Value);
            return uriBuilder.Uri;
        }
    }
}
