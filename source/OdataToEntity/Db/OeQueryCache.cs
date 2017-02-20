using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using OdataToEntity.Parsers.UriCompare;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Db
{
    public sealed class QueryCacheItem
    {
        private readonly IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private readonly OeEntryFactory _entryFactory;
        private readonly Object _query;

        public QueryCacheItem(Object query, IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> constantToParameterMapper, OeEntryFactory entryFactory)
        {
            _query = query;
            _constantToParameterMapper = constantToParameterMapper;
            _entryFactory = entryFactory;
        }

        public IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> ConstantToParameterMapper => _constantToParameterMapper;
        public OeEntryFactory EntryFactory => _entryFactory;
        public Object Query => _query;
    }

    public sealed class OeQueryCache
    {
        private readonly List<KeyValuePair<OeParseUriContext, QueryCacheItem>> _cache;

        public OeQueryCache()
        {
            _cache = new List<KeyValuePair<OeParseUriContext, QueryCacheItem>>();
            AllowCache = true;
        }

        public void AddQuery(OeParseUriContext parseUriContext, Object query, IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> constantNodeNames)
        {
            var queryCacheItem = new QueryCacheItem(query, constantNodeNames, parseUriContext.EntryFactory);
            lock (_cache)
                _cache.Add(new KeyValuePair<OeParseUriContext, QueryCacheItem>(parseUriContext, queryCacheItem));
        }
        public QueryCacheItem GetQuery(OeParseUriContext parseUriContext, out IReadOnlyList<OeQueryCacheDbParameterValue> parameterValues)
        {
            parameterValues = null;
            lock (_cache)
            {
                foreach (KeyValuePair<OeParseUriContext, QueryCacheItem> cacheItem in _cache)
                {
                    var uriComparer = new OeODataUriComparer(cacheItem.Value.ConstantToParameterMapper);
                    if (uriComparer.Compare(cacheItem.Key, parseUriContext))
                    {
                        parameterValues = uriComparer.ParameterValues;
                        return cacheItem.Value;
                    }
                }
            }
            return null;
        }

        public bool AllowCache { get; set; }
    }
}
