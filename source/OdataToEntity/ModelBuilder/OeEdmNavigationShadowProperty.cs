using Microsoft.OData.Edm;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeEdmNavigationShadowProperty : IEdmNavigationProperty
    {
        private readonly IEdmNavigationProperty _edmNavigationProperty;
        private IEdmNavigationProperty _partner;

        public OeEdmNavigationShadowProperty(IEdmNavigationProperty edmNavigationProperty, PropertyInfo propertyInfo)
        {
            _edmNavigationProperty = edmNavigationProperty;
            PropertyInfo = propertyInfo;

            _partner = edmNavigationProperty.Partner;
        }

        internal void SetPartner(IEdmNavigationProperty partner)
        {
            _partner = partner;
        }

        public bool ContainsTarget => _edmNavigationProperty.ContainsTarget;
        public IEdmStructuredType DeclaringType => _edmNavigationProperty.DeclaringType;
        public string Name => _edmNavigationProperty.Name;
        public EdmOnDeleteAction OnDelete => _edmNavigationProperty.OnDelete;
        public IEdmNavigationProperty Partner => _partner;
        public PropertyInfo PropertyInfo { get; }
        public EdmPropertyKind PropertyKind => _edmNavigationProperty.PropertyKind;
        public IEdmReferentialConstraint ReferentialConstraint => _edmNavigationProperty.ReferentialConstraint;
        public IEdmTypeReference Type => _edmNavigationProperty.Type;
    }
}
