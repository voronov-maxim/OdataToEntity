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
        protected void GetJsonSchema()
        {
            base.HttpContext.Response.ContentType = "application/schema+json";
            var edmModel = (IEdmModel)base.HttpContext.RequestServices.GetService(typeof(IEdmModel));
            GetJsonSchema(edmModel, base.HttpContext.Response.Body);
        }
        private static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            using (var memoryStream = new MemoryStream()) //kestrel allow only async operation
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(memoryStream))
                    if (!CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out IEnumerable<EdmError> errors))
                        return false;

                memoryStream.Position = 0;
                memoryStream.CopyToAsync(stream);
            }

            return false;
        }
        private static void GetJsonSchema(IEdmModel edmModel, Stream stream)
        {
            using (var memoryStream = new MemoryStream()) //kestrel allow only async operation
            {
                var schemaGenerator = new ModelBuilder.OeJsonSchemaGenerator(edmModel);
                schemaGenerator.Generate(memoryStream);
                memoryStream.Position = 0;
                memoryStream.CopyToAsync(stream);
            }
        }
    }
}
