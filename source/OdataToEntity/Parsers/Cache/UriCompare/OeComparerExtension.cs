using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace OdataToEntity.Cache.UriCompare
{
    public static class OeComparerExtension
    {
        public static bool IsNavigationNextLink(this ExpandedNavigationSelectItem item)
        {
            var segment = (NavigationPropertySegment)item.PathToNavigationProperty.LastSegment;
            return segment.NavigationProperty.Type is IEdmCollectionTypeReference;
        }
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
