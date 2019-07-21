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
                    int i = request.Path.Value.IndexOf('(');
                    if (i == -1)
                    {
                        i = request.Path.Value.IndexOf("/$", StringComparison.Ordinal);
                        if (i == -1)
                            i = request.Path.Value.Length;
                        else if (request.Path.Value.EndsWith("/$batch", StringComparison.OrdinalIgnoreCase))
                            i++;

                        i = request.Path.Value.LastIndexOf('/', i - 1);
                    }
                    else
                        i = request.Path.Value.IndexOf('/', 1);

                    if (i > 1)
                        uriBuilder.Path = request.Path.Value.Substring(1, i - 1);
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
