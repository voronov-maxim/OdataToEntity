using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public sealed class OrderContextController : OeBaseController
    {
        public OrderContextController(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
                : base(dataAdapter, edmModel)
        {
        }

        public async Task GetOrders()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task ScalarFunction()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task ResetDb()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task ResetManyColumns()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task ScalarFunctionWithParameters()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task TableFunction()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
        public async Task TableFunctionWithParameters()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body);
        }
    }
}