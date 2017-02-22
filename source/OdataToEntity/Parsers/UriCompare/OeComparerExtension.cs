using Microsoft.OData.Edm;

namespace OdataToEntity.Parsers.UriCompare
{
    public static class OeComparerExtension
    {
        public static bool IsEqual(this IEdmTypeReference @this, IEdmTypeReference edmTypeReference)
        {
            if (@this == edmTypeReference)
                return true;
            if (@this == null || edmTypeReference == null)
                return false;

            return @this.Definition == edmTypeReference.Definition && @this.IsNullable == edmTypeReference.IsNullable;
        }
    }
}
