using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using OdataToEntity.Parsers.UriCompare;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Db
{
    public sealed class QueryCacheItem
    {
        private readonly Expression _countExpression;
        private readonly OeEntryFactory _entryFactory;
        private readonly Object _query;
        private readonly OePropertyAccessor[] _skipTokenAccessors;

        public QueryCacheItem(Object query, Expression countExpression, OeEntryFactory entryFactory, OePropertyAccessor[] skipTokenAccessors)
        {
            _query = query;
            _countExpression = countExpression;
            _entryFactory = entryFactory;
            _skipTokenAccessors = skipTokenAccessors;
        }

        public Expression CountExpression => _countExpression;
        public OeEntryFactory EntryFactory => _entryFactory;
        public Object Query => _query;
        public OePropertyAccessor[] SkipTokenAccessors => _skipTokenAccessors;
    }

    public sealed class OeQueryCache
    {
        private readonly ConcurrentDictionary<OeCacheContext, QueryCacheItem> _cache;

        public OeQueryCache()
        {
            _cache = new ConcurrentDictionary<OeCacheContext, QueryCacheItem>(new OeCacheContextEqualityComparer());
            AllowCache = true;
        }

        public void AddQuery(OeCacheContext cacheContext, Object query, Expression countExpression, OeEntryFactory entryFactory,
            OePropertyAccessor[] skipTokenAccessors)
        {
            var queryCacheItem = new QueryCacheItem(query, countExpression, entryFactory, skipTokenAccessors);
            _cache.TryAdd(cacheContext, queryCacheItem);
        }
        public QueryCacheItem GetQuery(OeCacheContext cacheContext)
        {
            QueryCacheItem cacheItem;
            _cache.TryGetValue(cacheContext, out cacheItem);
            return cacheItem;
        }

        public bool AllowCache { get; set; }
        public int CacheCount => _cache.Count;
    }
}
