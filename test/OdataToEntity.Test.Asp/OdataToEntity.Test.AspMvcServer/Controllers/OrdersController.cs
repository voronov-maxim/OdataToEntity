using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrdersController : OeControllerBase
    {
        public OrdersController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
            : base(dataAdapter, edmModel)
        {
        }

        [HttpDelete]
        public void Delete(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
        [HttpGet]
        public IActionResult Get()
        {
            Db.OeAsyncEnumerator asyncEnumerator = base.GetAsyncEnumerator(base.HttpContext, base.HttpContext.Response.Body);
            return base.OData(asyncEnumerator);
        }
        [HttpPatch]
        public void Patch(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
        [HttpPost]
        public void Post(OeDataContext dataContext, Model.Order order)
        {
            dataContext.Update(order);
        }
    }
}
