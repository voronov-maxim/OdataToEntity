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

    public sealed class OeCacheContext
    {
        private readonly IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private readonly IEdmEntitySet _entitySet;
        private readonly OeMetadataLevel _metadataLevel;
        private readonly bool _navigationNextLink;
        private readonly ODataUri _odataUri;
        private readonly IReadOnlyList<OeParseNavigationSegment> _parseNavigationSegments;
        private readonly OeSkipTokenParser _skipTokenParser;

        public OeCacheContext(OeQueryContext queryContext)
        {
            _odataUri = queryContext.ODataUri;
            _entitySet = queryContext.EntitySet;
            _parseNavigationSegments = queryContext.ParseNavigationSegments;
            _metadataLevel = queryContext.MetadataLevel;
            _navigationNextLink = queryContext.NavigationNextLink;
            _skipTokenParser = queryContext.SkipTokenParser;
        }
        public OeCacheContext(OeQueryContext queryContext, IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
            : this(queryContext)
        {
            _constantToParameterMapper = constantToParameterMapper;
        }


        public IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> ConstantToParameterMapper => _constantToParameterMapper;
        public IEdmEntitySet EntitySet => _entitySet;
        public OeMetadataLevel MetadataLevel => _metadataLevel;
        public bool NavigationNextLink => _navigationNextLink;
        public ODataUri ODataUri => _odataUri;
        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues { get; set; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments => _parseNavigationSegments;
        public OeSkipTokenParser SkipTokenParser => _skipTokenParser;
    }
}
