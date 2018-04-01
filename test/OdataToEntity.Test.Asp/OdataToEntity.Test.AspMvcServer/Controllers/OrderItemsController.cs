using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrderItemsController : OeControllerBase
    {
        public OrderItemsController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.OrderItem orderItem)
        {
            dataContext.Update(orderItem);
        }
        [HttpGet]
        public ODataResult<Model.OrderItem> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.OrderItem>(asyncEnumerator);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.OrderItem> Get(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.OrderItem>(asyncEnumerator);
        }
        [HttpGet("{id}/Order/Customer")]
        public ODataResult<Model.Customer> OrderCustomer(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Customer>(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> orderItemProperties)
        {
            dataContext.Update(orderItemProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.OrderItem orderItem)
        {
            dataContext.Update(orderItem);
        }
    }
}