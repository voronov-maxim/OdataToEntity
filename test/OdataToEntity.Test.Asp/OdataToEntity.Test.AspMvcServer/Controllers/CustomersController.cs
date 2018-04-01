using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class CustomersController : OeControllerBase
    {
        public CustomersController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
                : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Customer customer)
        {
            dataContext.Update(customer);
        }
        [HttpGet]
        public ODataResult<Model.Customer> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Customer>(asyncEnumerator);
        }
        [HttpGet("{country},{id}")]
        public ODataResult<Model.Customer> Get(String country, String id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Customer>(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> customerProperties)
        {
            dataContext.Update(customerProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.Customer customer)
        {
            dataContext.Update(customer);
        }
    }
}