using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public class ManyColumnsViewController : OeControllerBase
    {
        public ManyColumnsViewController(IEdmModel edmModel)
            : base(edmModel)
        {
        }

        public async Task Get()
        {
            await base.Get(base.HttpContext, base.HttpContext.Response.Body, false, 10);
        }
    }
}