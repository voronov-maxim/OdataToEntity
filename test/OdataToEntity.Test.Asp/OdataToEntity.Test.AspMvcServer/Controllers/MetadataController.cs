using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public class MetadataController : OeMetadataController
    {
        [Route("api/$metadata")]
        public void GetMetadata()
        {
            base.WriteMetadata();
        }
        [Route("api/$json-schema")]
        public void GetJson()
        {
            base.WriteJsonSchema();
        }
    }
}