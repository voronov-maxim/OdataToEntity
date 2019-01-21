using Microsoft.AspNetCore.Mvc;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OdataToEntity.AspNetCore
{
    public class OeMetadataController : ControllerBase
    {
        protected void GetCsdlSchema()
        {
            base.HttpContext.Response.ContentType = "application/xml";
            var edmModel = (IEdmModel)base.HttpContext.RequestServices.GetService(typeof(IEdmModel));
            GetCsdlSchema(edmModel, base.HttpContext.Response.Body);
        }
        private static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            using (XmlWriter xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true }))
                if (CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out _))
                    return true;

            return false;
        }
    }
}
