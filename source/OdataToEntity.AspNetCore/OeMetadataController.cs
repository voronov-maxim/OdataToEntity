using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.IO;

namespace OdataToEntity.AspNetCore
{
    public class OeMetadataController : ControllerBase
    {
        protected void WriteJsonSchema()
        {
            base.HttpContext.Response.ContentType = "application/schema+json";
            WriteJsonSchema(base.HttpContext.GetEdmModel(), base.HttpContext.Response.Body);
        }
        private static void WriteJsonSchema(IEdmModel edmModel, Stream stream)
        {
            using (var memoryStream = new MemoryStream()) //kestrel allow only async operation
            {
                var schemaGenerator = new ModelBuilder.OeJsonSchemaGenerator(edmModel);
                schemaGenerator.Generate(memoryStream);
                memoryStream.Position = 0;
                memoryStream.CopyToAsync(stream);
            }
        }
        protected void WriteMetadata()
        {
            base.HttpContext.Response.ContentType = "application/xml";
            WriteMetadata(base.HttpContext.GetEdmModel(), base.HttpContext.Response.Body);
        }
        private static void WriteMetadata(IEdmModel edmModel, Stream stream)
        {
            using (var memoryStream = new MemoryStream()) //kestrel allow only async operation
            {
                var writerSettings = new ODataMessageWriterSettings();
                writerSettings.EnableMessageStreamDisposal = false;
                IODataResponseMessage message = new Infrastructure.OeInMemoryMessage(memoryStream, null);
                using (var writer = new ODataMessageWriter((IODataResponseMessageAsync)message, writerSettings, edmModel))
                    writer.WriteMetadataDocument();

                memoryStream.Position = 0;
                memoryStream.CopyToAsync(stream);
            }
        }
    }
}
