using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class OrderContextController : OeControllerBase
    {
        public OrderContextController(IEdmModel edmModel)
                : base(edmModel)
        {
        }

        [HttpGet("dbo.GetOrders")]
        public ODataResult<Model.Order> GetOrders()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Order>(asyncEnumerator);
        }
        [HttpGet("dbo.ScalarFunction")]
        public async Task<IActionResult> ScalarFunction()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        [HttpPost("ResetDb")]
        public async Task<IActionResult> ResetDb()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        [HttpPost("ResetManyColumns")]
        public async Task<IActionResult> ResetManyColumns()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        [HttpGet("dbo.ScalarFunctionWithParameters/{id},{name},{status}")]
        public async Task<IActionResult> ScalarFunctionWithParameters(String id, String name, String status)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return await base.ODataScalar(asyncEnumerator);
        }
        [HttpGet("TableFunction")]
        public ODataResult<Model.Order> TableFunction()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Order>(asyncEnumerator);
        }
        [HttpGet("TableFunctionWithParameters/{id},{name},{status}")]
        public ODataResult<Model.Order> TableFunctionWithParameters(String id, String name, String status)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Order>(asyncEnumerator);
        }
    }
}