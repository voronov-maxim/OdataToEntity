using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api")]
    public sealed class ManyColumnsController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ManyColumnsController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete("[controller]")]
        public void Delete(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
        [HttpGet("[controller]")]
        public ODataResult<Model.ManyColumns> Get()
        {
            Query.OeModelBoundProvider modelBoundProvider = _httpContextAccessor.HttpContext.CreateModelBoundProvider();
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext, modelBoundProvider);
            IAsyncEnumerable<Model.ManyColumns> manyColumns = parser.ExecuteReader<Model.ManyColumns>();
            return parser.OData(manyColumns);
        }
        [HttpGet("[controller]({id})")]
        public ODataResult<Model.ManyColumns> Get(int id)
        {
            var parser = new OeAspQueryParser(_httpContextAccessor.HttpContext);
            IAsyncEnumerable<Model.ManyColumns> manyColumns = parser.ExecuteReader<Model.ManyColumns>();
            return parser.OData(manyColumns);
        }
        [HttpPatch("[controller]")]
        public void Patch(OeDataContext dataContext, IDictionary<String, Object> manyColumnsProperties)
        {
            dataContext.Update(manyColumnsProperties);
        }
        [HttpPost("[controller]")]
        public void Post(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
    }
}