using Microsoft.OData;
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

        public FilterClause Filter { get; }
        public NavigationPropertySegment NavigationSegment { get; }
    }

    public sealed class OeCacheContext
    {
        public OeCacheContext(OeQueryContext queryContext)
        {
            ODataUri = queryContext.ODataUri;
            EntitySet = queryContext.EntitySet;
            ParseNavigationSegments = queryContext.ParseNavigationSegments;
            MetadataLevel = queryContext.MetadataLevel;
            NavigationNextLink = queryContext.NavigationNextLink;
            SkipTokenParser = queryContext.SkipTokenParser;
        }
        public OeCacheContext(OeQueryContext queryContext, IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
            : this(queryContext)
        {
            ConstantToParameterMapper = constantToParameterMapper;
        }


        public IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> ConstantToParameterMapper { get; }
        public IEdmEntitySet EntitySet { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues { get; set; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public OeSkipTokenParser SkipTokenParser { get; }
    }
}
