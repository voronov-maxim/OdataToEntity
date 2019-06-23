using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public readonly struct DynamicDependentPropertyInfo
    {
        public DynamicDependentPropertyInfo(String principalEntityName, String dependentEntityName,
            IReadOnlyList<String> principalPropertyNames, IReadOnlyList<String> dependentPropertyNames, bool isCollection)
        {
            PrincipalEntityName = principalEntityName;
            DependentEntityName = dependentEntityName;
            PrincipalPropertyNames = principalPropertyNames;
            DependentPropertyNames = dependentPropertyNames;
            IsCollection = isCollection;
        }

        public String DependentEntityName { get; }
        public IReadOnlyList<String> DependentPropertyNames { get; }
        public bool IsCollection { get; }
        public String PrincipalEntityName { get; }
        public IReadOnlyList<String> PrincipalPropertyNames { get; }
    }
}
