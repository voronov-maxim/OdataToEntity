using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;

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
        public IActionResult Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.ManyColumns manyColumns)
        {
            dataContext.Update(manyColumns);
        }
    }
}