using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class CustomerShippingAddressController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerShippingAddressController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete("[controller]")]
        public void Delete(OeDataContext dataContext, Model.CustomerShippingAddress customerShippingAddress)
        {
            dataContext.Update(customerShippingAddress);
        }
        [HttpGet("[controller]")]
        public ODataResult<Model.CustomerShippingAddress> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.CustomerShippingAddress> customerShippingAddresses = parser.ExecuteReader<Model.CustomerShippingAddress>();
            return parser.OData(customerShippingAddresses);
        }
        [HttpGet("[controller]({customerCountry},{customerId},{shippingAddressOrderId},{shippingAddressId})")]
        public ODataResult<Model.CustomerShippingAddress> Get(String customerCountry, String customerId, int shippingAddressOrderId, int shippingAddressId)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.CustomerShippingAddress> customerShippingAddresses = parser.ExecuteReader<Model.CustomerShippingAddress>();
            return parser.OData(customerShippingAddresses);
        }
        [HttpPatch("[controller]")]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> customerShippingAddressProperties)
        {
            dataContext.Update(customerShippingAddressProperties);
        }
        [HttpPost("[controller]")]
        public void Post(OeDataContext dataContext, Model.CustomerShippingAddress customerShippingAddress)
        {
            dataContext.Update(customerShippingAddress);
        }
    }
}
