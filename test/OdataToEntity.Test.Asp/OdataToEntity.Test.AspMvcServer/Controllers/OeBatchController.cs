using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/$batch")]
    public class OeBatchController : Controller
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;
        private static readonly Uri _rootUri = new Uri("http://dummy");

        public OeBatchController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }

        public async Task Post()
        {
            base.HttpContext.Response.ContentType = base.HttpContext.Request.ContentType;

            var apiSegment = base.HttpContext.Request.Path.Value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            var parser = new OeParser(apiSegment.Length == 1 ? _rootUri : new Uri(_rootUri, apiSegment[0]), _dataAdapter, _edmModel);

            await parser.ExecuteBatchAsync(base.HttpContext.Request.Body, base.HttpContext.Response.Body,
                base.HttpContext.Request.ContentType, CancellationToken.None);
        }
    }
}
