using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrderItemsViewController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderItemsViewController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet]
        public ODataResult<Model.OrderItemsView> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.OrderItemsView> orderItemsViews = parser.ExecuteReader<Model.OrderItemsView>();
            return parser.OData(orderItemsViews);
        }
    }
}