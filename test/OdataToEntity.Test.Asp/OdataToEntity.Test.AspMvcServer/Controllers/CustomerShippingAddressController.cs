using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class CustomerShippingAddressController : OeControllerBase
    {
        public CustomerShippingAddressController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
                : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.CustomerShippingAddress customerShippingAddress)
        {
            dataContext.Update(customerShippingAddress);
        }
        [HttpGet]
        public ODataResult<Model.CustomerShippingAddress> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.CustomerShippingAddress>(asyncEnumerator);
        }
        [HttpGet("{customerCountry},{customerId},{shippingAddressOrderId},{shippingAddressId}")]
        public ODataResult<Model.CustomerShippingAddress> Get(String customerCountry, String customerId, int shippingAddressOrderId, int shippingAddressId)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.CustomerShippingAddress>(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> customerShippingAddressProperties)
        {
            dataContext.Update(customerShippingAddressProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.CustomerShippingAddress customerShippingAddress)
        {
            dataContext.Update(customerShippingAddress);
        }
    }
}
