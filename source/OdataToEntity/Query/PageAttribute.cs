using System;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class PageAttribute : Attribute
    {
        public int MaxTop { get; set; }
        public int PageSize { get; set; }
        public bool NavigationNextLink { get; set; }
    }
}
