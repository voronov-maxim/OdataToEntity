using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class CategoriesController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CategoriesController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Category category)
        {
            dataContext.Update(category);
        }
        [HttpGet]
        public ODataResult<Model.Category> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.Category> categories = parser.ExecuteReader<Model.Category>();
            return parser.OData(categories);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.Category> Get(int id)
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.Category> categories = parser.ExecuteReader<Model.Category>();
            return parser.OData(categories);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> categoryProperties)
        {
            dataContext.Update(categoryProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.Category category)
        {
            dataContext.Update(category);
        }
    }
}