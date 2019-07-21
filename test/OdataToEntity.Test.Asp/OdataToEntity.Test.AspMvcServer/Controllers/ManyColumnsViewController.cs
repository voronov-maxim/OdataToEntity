using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/[controller]")]
    public class ManyColumnsViewController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ManyColumnsViewController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task Get()
        {
            var edmModel = (IEdmModel)_httpContextAccessor.HttpContext.RequestServices.GetService(typeof(IEdmModel));
            Query.OeModelBoundProvider modelBoundProvider = OeAspHelper.CreateModelBoundProvider(edmModel, 10, false);
            await OeAspQueryParser.Get(_httpContextAccessor.HttpContext, modelBoundProvider).ConfigureAwait(false);
        }
    }
}