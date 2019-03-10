using System;
using System.Collections;
using System.Reflection;

namespace OdataToEntity.Test
{
    public readonly struct EfInclude
    {
        public EfInclude(PropertyInfo property, Func<IEnumerable, IList> filter)
        {
            Property = property;
            Filter = filter;
        }

        public Func<IEnumerable, IList> Filter { get; }
        public PropertyInfo Property { get; }
    }
}
