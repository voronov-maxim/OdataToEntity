using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class OrderContextController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderContextController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("dbo.GetOrders")]
        public ODataResult<Model.Order> GetOrders()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpGet("dbo.ScalarFunction")]
        public async Task<IActionResult> ScalarFunction()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            int? result = await parser.ExecuteScalar<int>();
            return parser.OData(result);
        }
        [HttpPost("ResetDb")]
        public async Task ResetDb()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            await parser.ExecuteScalar<int>();
        }
        [HttpPost("ResetManyColumns")]
        public async Task ResetManyColumns()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            await parser.ExecuteScalar<int>();
        }
        [HttpGet("dbo.ScalarFunctionWithParameters/{id},{name},{status}")]
        public async Task<IActionResult> ScalarFunctionWithParameters(String id, String name, String status)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            int? result = await parser.ExecuteScalar<int>();
            return parser.OData(result);
        }
        [HttpGet("TableFunction")]
        public ODataResult<Model.Order> TableFunction()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpGet("TableFunctionWithParameters/{id},{name},{status}")]
        public ODataResult<Model.Order> TableFunctionWithParameters(String id, String name, String status)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
    }
}