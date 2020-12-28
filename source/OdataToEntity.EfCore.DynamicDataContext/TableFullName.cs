using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public readonly struct TableFullName : IEquatable<TableFullName>
    {
        public static readonly IEqualityComparer<TableFullName> OrdinalComparer = new OrdinalComparerImpl();
        public static readonly IEqualityComparer<TableFullName> OrdinalIgnoreCaseComparer = new OrdinalIgnoreCaseComparerImpl();

        private sealed class OrdinalComparerImpl : IEqualityComparer<TableFullName>
        {
            public bool Equals(TableFullName x, TableFullName y)
            {
                return x.Equals(y);
            }
            public int GetHashCode(TableFullName obj)
            {
                return obj.GetHashCode();
            }
        }

        private sealed class OrdinalIgnoreCaseComparerImpl : IEqualityComparer<TableFullName>
        {
            public bool Equals(TableFullName x, TableFullName y)
            {
                return String.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) == 0 &&
                    String.Compare(x.Schema, y.Schema, StringComparison.InvariantCultureIgnoreCase) == 0;
            }
            public int GetHashCode(TableFullName obj)
            {
                return obj.GetHashCode();
            }
        }

        public TableFullName(String schema, String name)
        {
            Schema = schema;
            Name = name;
        }

        public override bool Equals(Object? obj)
        {
            return obj is TableFullName tableFullName && Equals(tableFullName);
        }
        public bool Equals(TableFullName other)
        {
            return String.CompareOrdinal(Name, other.Name) == 0 && String.CompareOrdinal(Schema, other.Schema) == 0;
        }
        public override int GetHashCode()
        {
            int h1 = Name.GetHashCode();
            int h2 = Schema.GetHashCode();
            uint num = (uint)(h1 << 5) | ((uint)h1 >> 27);
            return ((int)num + h1) ^ h2;
        }
        public override String ToString()
        {
            return "(" + Schema + "." + Name + ")";
        }

        public String Name { get; }
        public String Schema { get; }
    }
}
