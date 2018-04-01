using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class CategoriesController : OeControllerBase
    {
        public CategoriesController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Category category)
        {
            dataContext.Update(category);
        }
        [HttpGet]
        public ODataResult<Model.Category> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Category>(asyncEnumerator);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.Category> Get(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.Category>(asyncEnumerator);
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