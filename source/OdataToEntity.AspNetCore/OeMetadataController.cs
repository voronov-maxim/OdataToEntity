using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OdataToEntity.AspNetCore
{
    public class OeMetadataController : Controller
    {
        protected void GetCsdlSchema()
        {
            base.HttpContext.Response.ContentType = "application/xml";
            var edmModel = (IEdmModel)base.HttpContext.RequestServices.GetService(typeof(IEdmModel));
            GetCsdlSchema(edmModel, base.HttpContext.Response.Body);
        }
        private static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            using (XmlWriter xmlWriter = XmlWriter.Create(stream))
                if (CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out IEnumerable<EdmError> errors))
                    return true;

            return false;
        }
    }
}
