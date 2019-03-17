using System;
using System.Collections;
using System.Reflection;

namespace OdataToEntity.Test
{
    public readonly struct EfInclude
    {
        public EfInclude(PropertyInfo property, Func<IEnumerable, IList> filter) : this(property, filter, null)
        {
        }
        public EfInclude(PropertyInfo property, Func<IEnumerable, IList> filter, PropertyInfo parentProperty)
        {
            Property = property;
            Filter = filter;
            ParentProperty = parentProperty;
        }

        public Func<IEnumerable, IList> Filter { get; }
        public PropertyInfo ParentProperty { get; }
        public PropertyInfo Property { get; }
    }
}
