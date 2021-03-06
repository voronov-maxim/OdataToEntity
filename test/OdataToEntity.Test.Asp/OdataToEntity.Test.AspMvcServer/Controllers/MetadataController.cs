using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public class MetadataController : OeMetadataController
    {
        [Route("api/$metadata")]
        public async Task GetMetadata()
        {
            await base.WriteMetadataAsync().ConfigureAwait(false);
        }
        [Route("api/$json-schema")]
        public void GetJson()
        {
            base.WriteJsonSchema();
        }
    }
}