using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrderContextController : OeControllerBase
    {
        public OrderContextController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
                : base(dataAdapter, edmModel)
        {
        }

        public IActionResult GetOrders()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
        public async Task<IActionResult> ScalarFunction()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        public async Task<IActionResult> ResetDb()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        public async Task<IActionResult> ResetManyColumns()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        public async Task<IActionResult> ScalarFunctionWithParameters()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        public IActionResult TableFunction()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
        public IActionResult TableFunctionWithParameters()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
    }
}