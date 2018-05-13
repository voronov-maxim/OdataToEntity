using Microsoft.OData.UriParser;

namespace OdataToEntity.Parsers
{
    public readonly struct OeParseNavigationSegment
    {
        public OeParseNavigationSegment(NavigationPropertySegment navigationSegment, FilterClause filter)
        {
            NavigationSegment = navigationSegment;
            Filter = filter;
        }

        public FilterClause Filter { get; }
        public NavigationPropertySegment NavigationSegment { get; }
    }
}
