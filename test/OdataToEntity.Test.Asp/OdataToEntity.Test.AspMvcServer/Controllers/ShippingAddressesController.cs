using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class ShippingAddressesController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ShippingAddressesController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.ShippingAddress shippingAddress)
        {
            dataContext.Update(shippingAddress);
        }
        [HttpGet]
        public ODataResult<Model.ShippingAddress> Get()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.ShippingAddress> shippingAddresses = parser.ExecuteReader<Model.ShippingAddress>();
            return parser.OData(shippingAddresses);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.ShippingAddress> Get(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.ShippingAddress> shippingAddresses = parser.ExecuteReader<Model.ShippingAddress>();
            return parser.OData(shippingAddresses);
        }
        [HttpGet("{id}/Order/Customer")]
        public ODataResult<Model.Customer> OrderCustomer(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Customer> customers = parser.ExecuteReader<Model.Customer>();
            return parser.OData(customers);
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
