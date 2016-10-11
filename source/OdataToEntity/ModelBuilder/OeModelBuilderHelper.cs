using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OdataToEntity.ModelBuilder
{
    public static class OeModelBuilderHelper
    {
        public static bool GetCsdlSchema(IEdmModel edmModel, Stream stream)
        {
            IEnumerable<EdmError> errors;
            using (XmlWriter xmlWriter = XmlWriter.Create(stream))
                if (CsdlWriter.TryWriteCsdl(edmModel, xmlWriter, CsdlTarget.OData, out errors))
                    return true;

            return false;
        }

    }
}
