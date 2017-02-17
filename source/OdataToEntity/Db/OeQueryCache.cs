using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using OdataToEntity.Parsers.UriCompare;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Db
{
    public sealed class QueryCacheItem
    {
        private readonly IReadOnlyDictionary<ConstantNode, String> _constantNodeNames;
        private readonly OeEntryFactory _entryFactory;
        private readonly Object _query;

        public QueryCacheItem(Object query, IReadOnlyDictionary<ConstantNode, String> constantNodeNames, OeEntryFactory entryFactory)
        {
            _query = query;
            _constantNodeNames = constantNodeNames;
            _entryFactory = entryFactory;
        }

        public IReadOnlyDictionary<ConstantNode, String> ConstantNodeNames => _constantNodeNames;
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

        public void AddQuery(OeParseUriContext parseUriContext, Object query, IReadOnlyDictionary<ConstantNode, String> constantNodeNames)
        {
            var queryCacheItem = new QueryCacheItem(query, constantNodeNames, parseUriContext.EntryFactory);
            _cache.Add(new KeyValuePair<OeParseUriContext, QueryCacheItem>(parseUriContext, queryCacheItem));
        }
        public QueryCacheItem GetQuery(OeParseUriContext parseUriContext, out IReadOnlyList<KeyValuePair<String, Object>> parameterValues)
        {
            parameterValues = null;
            foreach (KeyValuePair<OeParseUriContext, QueryCacheItem> cacheItem in _cache)
            {
                var uriComparer = new OeODataUriComparer(cacheItem.Value.ConstantNodeNames);
                if (uriComparer.Compare(cacheItem.Key, parseUriContext))
                {
                    parameterValues = uriComparer.ParameterValues;
                    return cacheItem.Value;
                }
            }
            return null;
        }

        public bool AllowCache { get; set; }
    }
}
