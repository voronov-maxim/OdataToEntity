using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class CustomersController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomersController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("BoundFunctionCollection(orderNames={orderNames})", Order = 1)]
        public ODataResult<Model.Order> BoundFunctionCollection(String orderNames)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpGet("{country},{id}/OdataToEntity.Test.Model.BoundFunctionSingle(orderNames={orderNames})")]
        public ODataResult<Model.Order> BoundFunctionSingle(String country, String id, String orderNames)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Order> orders = parser.ExecuteReader<Model.Order>();
            return parser.OData(orders);
        }
        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Customer customer)
        {
            dataContext.Update(customer);
        }
        [HttpGet]
        public ODataResult<Model.Customer> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.Customer> customers = parser.ExecuteReader<Model.Customer>();
            return parser.OData(customers);
        }
        [HttpGet("{country},{id}", Order = 2)]
        public ODataResult<Model.Customer> Get(String country, String id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.Customer> customers = parser.ExecuteReader<Model.Customer>();
            return parser.OData(customers);
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