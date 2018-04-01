using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class ManyColumnsController : OeControllerBase
    {
        public ManyColumnsController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
        [HttpGet]
        public ODataResult<Model.ManyColumns> Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.ManyColumns>(asyncEnumerator);
        }
        [HttpGet("{id}")]
        public ODataResult<Model.ManyColumns> Get(int id)
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData<Model.ManyColumns>(asyncEnumerator);
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