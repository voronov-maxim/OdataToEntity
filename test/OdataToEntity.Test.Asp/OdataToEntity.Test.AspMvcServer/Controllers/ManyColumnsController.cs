using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class ManyColumnsController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ManyColumnsController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
        [HttpGet]
        public ODataResult<Model.ManyColumns> Get()
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.ManyColumns> manyColumns = parser.ExecuteReader<Model.ManyColumns>();
            return parser.OData(manyColumns);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.ManyColumns> Get(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.ManyColumns> manyColumns = parser.ExecuteReader<Model.ManyColumns>();
            return parser.OData(manyColumns);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> manyColumnsProperties)
        {
            dataContext.Update(manyColumnsProperties);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
    }
}