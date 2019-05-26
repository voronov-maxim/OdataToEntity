using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicMetadataProvider
    {
        public abstract DynamicDependentPropertyInfo GetDependentProperties(String tableName, String navigationPropertyName);
        public abstract String GetEntityName(String tableName);
        public abstract IEnumerable<(String, String)> GetManyToManyProperties(String tableName);
        public abstract IEnumerable<String> GetNavigationProperties(String tableName);
        public abstract IEnumerable<String> GetPrimaryKey(String tableName);
        public abstract IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableName);
        public abstract String GetTableName(String entityName);
        public abstract IEnumerable<String> GetTableNames();
    }
}
