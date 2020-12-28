using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public readonly struct DynamicDependentPropertyInfo
    {
        public DynamicDependentPropertyInfo(in TableFullName principalTableName, in TableFullName dependentTableName,
            IReadOnlyList<String> principalPropertyNames, IReadOnlyList<String> dependentPropertyNames, bool isCollection)
        {
            PrincipalTableName = principalTableName;
            DependentTableName = dependentTableName;
            PrincipalPropertyNames = principalPropertyNames;
            DependentPropertyNames = dependentPropertyNames;
            IsCollection = isCollection;
        }

        public TableFullName DependentTableName { get; }
        public IReadOnlyList<String> DependentPropertyNames { get; }
        public bool IsCollection { get; }
        public TableFullName PrincipalTableName { get; }
        public IReadOnlyList<String> PrincipalPropertyNames { get; }
    }
}
