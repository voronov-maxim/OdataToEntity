using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.IO;
using System.Threading.Tasks;

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
        protected Task WriteMetadataAsync()
        {
            base.HttpContext.Response.ContentType = "application/xml";
            return WriteMetadataAsync(base.HttpContext.GetEdmModel(), base.HttpContext.Response.Body);
        }
        private static async Task WriteMetadataAsync(IEdmModel edmModel, Stream stream)
        {
            var writerSettings = new ODataMessageWriterSettings();
            writerSettings.EnableMessageStreamDisposal = false;
            IODataResponseMessage message = new Infrastructure.OeInMemoryMessage(stream, null);
            using (var writer = new ODataMessageWriter((IODataResponseMessageAsync)message, writerSettings, edmModel))
                await writer.WriteMetadataDocumentAsync().ConfigureAwait(false);
        }
    }
}
