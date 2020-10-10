using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrdersController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrdersController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("$count")]
        public async Task<string> Count()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            Model.OrderContext orderContext = parser.GetDbContext<Model.OrderContext>();
            int? count = await parser.ExecuteScalar<int>(orderContext.Orders).ConfigureAwait(false);
            return count.ToString();
        }
        [HttpDelete("{id}")]
        public void Delete(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
        [HttpGet]
        public async Task<ODataResult<Model.Order>> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            Model.OrderContext orderContext = parser.GetDbContext<Model.OrderContext>();
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>(orderContext.Orders.AsQueryable().Where(o => o.Id > 0));
            List<Model.Order> orderList = await orders.OrderBy(o => o.Id).ToListAsync().ConfigureAwait(false);
            return parser.OData(orderList);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.Order> Get(int id)
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpGet("{id}/Items")]
        public ODataResult<Model.OrderItem> GetItems(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.OrderItem> orderItems = parser.ExecuteReader<Model.OrderItem>();
            return parser.OData(orderItems);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> orderProperties)
        {
            dataContext.Update(orderProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
    }
}
