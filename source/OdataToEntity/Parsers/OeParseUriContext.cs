using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public struct OeParseNavigationSegment
    {
        private readonly FilterClause _filter;
        private readonly NavigationPropertySegment _navigationSegment;

        public OeParseNavigationSegment(NavigationPropertySegment navigationSegment, FilterClause filter)
        {
            _navigationSegment = navigationSegment;
            _filter = filter;
        }

        public FilterClause Filter => _filter;
        public NavigationPropertySegment NavigationSegment => _navigationSegment;
    }

    public struct OeParseUriContext
    {
        private IEdmEntitySet _entitySet;
        private readonly IReadOnlyList<OeParseNavigationSegment> _parseNavigationSegments;
        private readonly ODataUri _odataUri;

        public OeParseUriContext(ODataUri odataUri, IEdmEntitySet entitySet, IReadOnlyList<OeParseNavigationSegment> parseNavigationSegments)
        {
            _odataUri = odataUri;
            _entitySet = entitySet;
            _parseNavigationSegments = parseNavigationSegments;
        }


        public IEdmEntitySet EntitySet => _entitySet;
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments => _parseNavigationSegments;
        public ODataUri ODataUri => _odataUri;
    }
}
