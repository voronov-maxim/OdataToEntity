using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmStructuralProperty : EdmStructuralProperty
    {
        public OeEdmStructuralProperty(IEdmStructuredType declaringType, String name, IEdmTypeReference type)
            : base(declaringType, name, type)
        {
        }
    }
}
