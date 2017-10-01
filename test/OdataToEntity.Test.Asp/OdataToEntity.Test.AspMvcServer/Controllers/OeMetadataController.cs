using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OdataToEntity.Test.AspMvcServer.Controllers
{
    [Route("api/$metadata")]
    public class OeMetadataController : Controller
    {
        private readonly IEdmModel _edmModel;

        public OeMetadataController(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public void Get()
        {
            base.HttpContext.Response.ContentType = "application/xml";
            GetCsdlSchema(_edmModel, base.HttpContext.Response.Body);
        }
        private static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            IEnumerable<EdmError> errors;
            using (XmlWriter xmlWriter = XmlWriter.Create(stream))
                if (CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out errors))
                    return true;

            return false;
        }
    }
}
