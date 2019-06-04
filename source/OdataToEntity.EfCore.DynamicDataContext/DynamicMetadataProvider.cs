using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicMetadataProvider
    {
        public abstract DynamicDependentPropertyInfo GetDependentProperties(String tableEdmName, String navigationPropertyName);
        public abstract String GetEntityName(String tableEdmName);
        public abstract IEnumerable<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName);
        public abstract IEnumerable<String> GetNavigationProperties(String tableEdmName);
        public abstract IEnumerable<String> GetPrimaryKey(String tableEdmName);
        public abstract IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableEdmName);
        public abstract String GetTableName(String entityName);
        public abstract IEnumerable<(String tableEdmName, bool isQueryType)> GetTableNames();

        public abstract DbContextOptions DbContextOptions { get; }
    }
}
