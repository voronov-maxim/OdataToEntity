using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
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
        public IActionResult Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, Model.Category category)
        {
            dataContext.Update(category);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.Category category)
        {
            dataContext.Update(category);
        }
    }
}