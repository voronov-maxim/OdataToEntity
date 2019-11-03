using Microsoft.OData;
using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Cache
{
    public sealed class OeCacheContext
    {
        private static readonly Dictionary<ConstantNode, OeQueryCacheDbParameterDefinition> Empty = new Dictionary<ConstantNode, OeQueryCacheDbParameterDefinition>();

        public OeCacheContext(OeQueryContext queryContext)
        {
            ODataUri = queryContext.ODataUri;
            ParseNavigationSegments = queryContext.ParseNavigationSegments;
            MetadataLevel = queryContext.MetadataLevel;
            SkipTokenNameValues = queryContext.SkipTokenNameValues;
            ConstantToParameterMapper = Empty;
            ParameterValues = Array.Empty<OeQueryCacheDbParameterValue>();
        }
        public OeCacheContext(OeQueryContext queryContext, IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> constantToParameterMapper)
            : this(queryContext)
        {
            ConstantToParameterMapper = constantToParameterMapper;
        }


        public IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> ConstantToParameterMapper { get; }
        public OeMetadataLevel MetadataLevel { get; }
        public bool NavigationNextLink { get; }
        public ODataUri ODataUri { get; }
        public IReadOnlyList<OeQueryCacheDbParameterValue> ParameterValues { get; set; }
        public IReadOnlyList<OeParseNavigationSegment> ParseNavigationSegments { get; }
        public OeSkipTokenNameValue[] SkipTokenNameValues { get; }
    }
}
