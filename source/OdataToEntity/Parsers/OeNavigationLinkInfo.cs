using System;
using System.Collections.Generic;
using System.Text;

namespace OdataToEntity.Parsers
{
    internal abstract class OeNavigationLinkInfo
    {
        public OeNavigationLinkInfo(object collection, int count)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            Collection = collection;
            Count = count;
        }
        public object Collection { get; }
        public int Count { get; }
    }

    internal class OeNavigationLinkInfo<T> : OeNavigationLinkInfo
    {
        public OeNavigationLinkInfo(T collection, int count) : base(collection, count)
        {
        }

        public new T Collection { get { return (T)base.Collection; } }
    }
}
