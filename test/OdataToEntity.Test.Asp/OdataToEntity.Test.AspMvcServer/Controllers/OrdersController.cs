using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrdersController : OeBaseController
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
        public async Task Get()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
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
