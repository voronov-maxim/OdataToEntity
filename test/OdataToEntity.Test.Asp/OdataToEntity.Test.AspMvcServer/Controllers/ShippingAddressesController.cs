using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class ShippingAddressesController : OeControllerBase
    {
        public ShippingAddressesController(IEdmModel edmModel)
            : base(edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.ShippingAddress shippingAddress)
        {
            dataContext.Update(shippingAddress);
        }
        [HttpGet]
        public ODataResult<Model.ShippingAddress> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.ShippingAddress>(asyncEnumerator);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.ShippingAddress> Get(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.ShippingAddress>(asyncEnumerator);
        }
        [HttpGet("{id}/Order/Customer")]
        public ODataResult<Model.Customer> OrderCustomer(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Customer>(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> shippingAddressProperties)
        {
            dataContext.Update(shippingAddressProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.ShippingAddress shippingAddress)
        {
            dataContext.Update(shippingAddress);
        }
    }
}
