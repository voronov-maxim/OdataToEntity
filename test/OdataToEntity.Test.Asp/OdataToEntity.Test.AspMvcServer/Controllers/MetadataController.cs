using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    public class MetadataController : OeMetadataController
    {
        [Route("api/$metadata")]
        public void GetCsdl()
        {
            base.GetCsdlSchema();
        }
        [Route("api/$json-schema")]
        public void GetJson()
        {
            base.GetJsonSchema();
        }
    }
}