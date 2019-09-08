using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class OrdersController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrdersController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete("[controller]")]
        public void Delete(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
        [HttpGet("[controller]")]
        public async Task<ODataResult<Model.Order>> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            Model.OrderContext orderContext = parser.GetDbContext<Model.OrderContext>();
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>(orderContext.Orders.AsQueryable().Where(o => o.Id > 0));
            List<Model.Order> orderList = await orders.OrderBy(o => o.Id).ToListAsync().ConfigureAwait(false);
            return parser.OData(orderList);
        }
        [HttpGet("[controller]({id})")]
        public ODataResult<Model.Order> Get(int id)
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpGet("[controller]({id})/Items")]
        public ODataResult<Model.OrderItem> GetItems(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.OrderItem> orderItems = parser.ExecuteReader<Model.OrderItem>();
            return parser.OData(orderItems);
        }
        [HttpPatch("[controller]")]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> orderProperties)
        {
            dataContext.Update(orderProperties);
        }
        [HttpPost("[controller]")]
        public void Post(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
    }
}
