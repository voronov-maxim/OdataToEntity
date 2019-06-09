using System;
using System.Collections;
using System.Reflection;

namespace OdataToEntity.Test
{
    public readonly struct EfInclude
    {
        public EfInclude(PropertyInfo property, Func<IEnumerable, IList> filter, bool isOrdered) : this(property, filter, isOrdered, null)
        {
        }
        public EfInclude(PropertyInfo property, Func<IEnumerable, IList> filter, bool isOrdered, PropertyInfo parentProperty)
        {
            Property = property;
            Filter = filter;
            ParentProperty = parentProperty;
            IsOrdered = isOrdered;
        }

        public Func<IEnumerable, IList> Filter { get; }
        public bool IsOrdered { get; }
        public PropertyInfo ParentProperty { get; }
        public PropertyInfo Property { get; }
    }
}
