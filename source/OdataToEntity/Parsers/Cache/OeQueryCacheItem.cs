using OdataToEntity.Parsers;
using System;
using System.Linq.Expressions;

namespace OdataToEntity.Cache
{
    public sealed class OeQueryCacheItem
    {
        public OeQueryCacheItem(Object query, MethodCallExpression countExpression, OeEntryFactory entryFactory, OePropertyAccessor[] skipTokenAccessors)
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
}
