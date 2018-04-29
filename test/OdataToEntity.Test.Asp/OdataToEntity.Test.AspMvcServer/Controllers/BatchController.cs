using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/$batch")]
    public sealed class BatchController : OeBatchController
    {
        protected override Task BatchCore()
        {
            return base.BatchCore();
        }
        protected override void OnBeforeInvokeController(OeDataContext dataContext, ODataResource entry)
        {
        }
        protected override Task<int> SaveChangesAsync(object dataContext)
        {
            return base.SaveChangesAsync(dataContext);
        }
    }
}