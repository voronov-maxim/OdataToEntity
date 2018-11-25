using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public readonly struct OeParseNavigationSegment
    {
        public OeParseNavigationSegment(NavigationPropertySegment navigationSegment, FilterClause filter)
        {
            NavigationSegment = navigationSegment;
            Filter = filter;
        }

        public static IEdmEntitySet GetEntitySet(IReadOnlyList<OeParseNavigationSegment> navigationSegments)
        {
            if (navigationSegments != null)
                for (int i = navigationSegments.Count - 1; i >= 0; i--)
                    if (navigationSegments[i].NavigationSegment != null)
                        return (IEdmEntitySet)navigationSegments[i].NavigationSegment.NavigationSource;

            return null;
        }

        public FilterClause Filter { get; }
        public NavigationPropertySegment NavigationSegment { get; }
    }
}
