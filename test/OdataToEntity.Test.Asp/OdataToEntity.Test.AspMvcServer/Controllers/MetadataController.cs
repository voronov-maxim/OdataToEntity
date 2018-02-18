using Microsoft.AspNetCore.Mvc;
using OdataToEntity.AspNetCore;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/$metadata")]
    public class MetadataController : OeMetadataController
    {
        public void Get()
        {
            base.GetCsdlSchema();
        }
    }
}