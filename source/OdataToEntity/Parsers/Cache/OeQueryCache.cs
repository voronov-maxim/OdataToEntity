using OdataToEntity.Parsers;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace OdataToEntity.Cache
{
    public sealed class OeQueryCache
    {
        private readonly ConcurrentDictionary<OeCacheContext, OeQueryCacheItem> _cache;

        public OeQueryCache() : this(true)
        {
        }
        public OeQueryCache(bool allowCache)
        {
            _cache = new ConcurrentDictionary<OeCacheContext, OeQueryCacheItem>(new OeCacheContextEqualityComparer());
            AllowCache = allowCache;
        }

        public void AddQuery(OeCacheContext cacheContext, Object query, MethodCallExpression? countExpression, OeEntryFactory? entryFactory)
        {
            var queryCacheItem = new OeQueryCacheItem(query, countExpression, entryFactory);
            _cache.TryAdd(cacheContext, queryCacheItem);
        }
        public OeQueryCacheItem? GetQuery(OeCacheContext cacheContext)
        {
            _cache.TryGetValue(cacheContext, out OeQueryCacheItem? cacheItem);
            return cacheItem;
        }

        public bool AllowCache { get; set; }
        public int CacheCount => _cache.Count;
    }
}
