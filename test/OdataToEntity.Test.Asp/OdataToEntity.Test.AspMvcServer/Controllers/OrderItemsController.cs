using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class OrderItemsController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderItemsController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete("[controller]")]
        public void Delete(OeDataContext dataContext, Model.OrderItem orderItem)
        {
            dataContext.Update(orderItem);
        }
        [HttpGet("[controller]")]
        public ODataResult<Model.OrderItem> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.OrderItem> orderItems = parser.ExecuteReader<Model.OrderItem>();
            return parser.OData(orderItems);
        }
        [HttpGet("[controller]({id})")]
        public ODataResult<Model.OrderItem> Get(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.OrderItem> orderItems = parser.ExecuteReader<Model.OrderItem>();
            return parser.OData(orderItems);
        }
        [HttpGet("[controller]({id})/Order/Customer")]
        public ODataResult<Model.Customer> OrderCustomer(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Customer> customers = parser.ExecuteReader<Model.Customer>();
            return parser.OData(customers);
        }
        [HttpPatch("[controller]")]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> orderItemProperties)
        {
            dataContext.Update(orderItemProperties);
        }
        [HttpPost("[controller]")]
        public void Post(OeDataContext dataContext, Model.OrderItem orderItem)
        {
            dataContext.Update(orderItem);
        }
    }
}