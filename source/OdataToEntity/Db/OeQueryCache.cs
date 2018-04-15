using OdataToEntity.Parsers;
using OdataToEntity.Parsers.UriCompare;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace OdataToEntity.Db
{
    public sealed class QueryCacheItem
    {
        public QueryCacheItem(Object query, MethodCallExpression countExpression, OeEntryFactory entryFactory, OePropertyAccessor[] skipTokenAccessors)
        {
            Query = query;
            CountExpression = countExpression;
            EntryFactory = entryFactory;
            SkipTokenAccessors = skipTokenAccessors;
        }

        public MethodCallExpression CountExpression { get; }
        public OeEntryFactory EntryFactory { get; }
        public Object Query { get; }
        public OePropertyAccessor[] SkipTokenAccessors { get; }
    }

    public sealed class OeQueryCache
    {
        private readonly ConcurrentDictionary<OeCacheContext, QueryCacheItem> _cache;

        public OeQueryCache() : this(true)
        {
        }
        public OeQueryCache(bool allowCache)
        {
            _cache = new ConcurrentDictionary<OeCacheContext, QueryCacheItem>(new OeCacheContextEqualityComparer());
            AllowCache = allowCache;
        }

        public void AddQuery(OeCacheContext cacheContext, Object query, MethodCallExpression countExpression, OeEntryFactory entryFactory,
            OePropertyAccessor[] skipTokenAccessors)
        {
            var queryCacheItem = new QueryCacheItem(query, countExpression, entryFactory, skipTokenAccessors);
            _cache.TryAdd(cacheContext, queryCacheItem);
        }
        public QueryCacheItem GetQuery(OeCacheContext cacheContext)
        {
            _cache.TryGetValue(cacheContext, out QueryCacheItem cacheItem);
            return cacheItem;
        }

        public bool AllowCache { get; set; }
        public int CacheCount => _cache.Count;
    }
}
