using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            await OeAspQueryParser.Get(_httpContextAccessor.HttpContext, 10);
        }
    }
}