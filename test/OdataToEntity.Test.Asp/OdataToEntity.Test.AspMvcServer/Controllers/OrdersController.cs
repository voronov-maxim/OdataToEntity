using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrdersController : OeControllerBase
    {
        public OrdersController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
        [HttpGet]
        public ODataResult<Model.Order> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Order>(asyncEnumerator);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.Order> Get(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Order>(asyncEnumerator);
        }
        [HttpGet("{id}/Items")]
        public ODataResult<Model.OrderItem> GetItems(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.OrderItem>(asyncEnumerator);
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
