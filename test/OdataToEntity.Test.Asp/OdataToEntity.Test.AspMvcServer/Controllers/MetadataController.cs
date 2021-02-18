using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public class MetadataController : OeMetadataController
    {
        [Route("api/$metadata")]
        public Task GetMetadata()
        {
            return base.WriteMetadata();
        }
        [Route("api/$json-schema")]
        public void GetJson()
        {
            base.WriteJsonSchema();
        }
    }
}