using Microsoft.OData.Edm;
using System;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmStructuralShadowProperty : EdmStructuralProperty
    {
        public OeEdmStructuralShadowProperty(IEdmStructuredType declaringType, String name, IEdmTypeReference type, PropertyInfo propertyInfo)
            : base(declaringType, name, type)
        {
            PropertyInfo = propertyInfo;
        }

        public PropertyInfo PropertyInfo { get; }
    }
}
