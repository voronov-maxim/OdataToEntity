using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicMetadataProvider : IDisposable
    {
        private readonly struct FKeyMapping
        {
            public FKeyMapping(NavigationMapping dependent, NavigationMapping principal)
            {
                Dependent = dependent;
                Principal = principal;
            }

            public NavigationMapping Dependent { get; }
            public NavigationMapping Principal { get; }
        }

        private readonly SchemaCache _schemaCache;

        public DynamicMetadataProvider(ProviderSpecificSchema informationSchema)
        {
            DynamicDbContextOptions = informationSchema.DynamicDbContextOptions;
            IsDatabaseNullHighestValue = informationSchema.IsDatabaseNullHighestValue;
            _schemaCache = new SchemaCache(informationSchema);
        }

        public void Dispose()
        {
            _schemaCache.Dispose();
        }
        public DynamicDependentPropertyInfo GetDependentProperties(String tableName, String navigationPropertyName)
        {
            (String tableSchema, String tableName, bool isQueryType) tableFullName = _schemaCache.GetTables()[tableName];
            if (_schemaCache.GetNavigations().TryGetValue((tableFullName.tableSchema, tableFullName.tableName), out List<SchemaCache.Navigation> navigations))
                foreach (SchemaCache.Navigation navigation in navigations)
                    if (navigation.NavigationName == navigationPropertyName)
                    {
                        List<KeyColumnUsage> dependent = _schemaCache.GetKeyColumns()[(navigation.ConstraintSchema, navigation.DependentConstraintName)];
                        List<KeyColumnUsage> principal = _schemaCache.GetKeyColumns()[(navigation.ConstraintSchema, navigation.PrincipalConstraintName)]; ;
                        List<String> principalPropertyNames = principal.OrderBy(p => p.OrdinalPosition).Select(p => p.ColumnName).ToList();
                        List<String> dependentPropertyNames = dependent.OrderBy(p => p.OrdinalPosition).Select(p => p.ColumnName).ToList();
                        return new DynamicDependentPropertyInfo(principal[0].TableName, dependent[0].TableName, principalPropertyNames, dependentPropertyNames, navigation.IsCollection);
                    }

            throw new InvalidOperationException("Navigation property " + navigationPropertyName + " not found in table " + tableName);
        }
        public String GetEntityName(String tableName)
        {
            return tableName;
        }
        public IEnumerable<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName)
        {
            foreach (NavigationMapping navigationMapping in _schemaCache.GetNavigationMappings(tableEdmName))
                if (!String.IsNullOrEmpty(navigationMapping.ManyToManyTarget))
                    yield return (navigationMapping.NavigationName, navigationMapping.ManyToManyTarget);
        }
        public IEnumerable<String> GetNavigationProperties(String tableEdmName)
        {
            (String tableSchema, String tableName, bool _) = _schemaCache.GetTables()[tableEdmName];
            if (_schemaCache.GetNavigations().TryGetValue((tableSchema, tableName), out List<SchemaCache.Navigation> navigations))
                foreach (SchemaCache.Navigation navigation in navigations)
                    yield return navigation.NavigationName;
        }
        public IEnumerable<String> GetPrimaryKey(String tableEdmName)
        {
            (String tableSchema, String tableName) tableFullName = _schemaCache.GetTableFullName(tableEdmName);
            String constraintName = _schemaCache.GetPrimaryKeyConstraintNames()[tableFullName];
            List<KeyColumnUsage> keyColumns = _schemaCache.GetKeyColumns()[(tableFullName.tableSchema, constraintName)];
            return keyColumns.OrderBy(c => c.OrdinalPosition).Select(c => c.ColumnName);
        }
        public IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableName)
        {
            foreach (Column column in _schemaCache.GetColumns(tableName))
            {
                DatabaseGeneratedOption databaseGenerated;
                if (column.IsIdentity)
                    databaseGenerated = DatabaseGeneratedOption.Identity;
                else if (column.IsComputed)
                    databaseGenerated = DatabaseGeneratedOption.Computed;
                else
                    databaseGenerated = DatabaseGeneratedOption.None;

                Type propertyType = column.ClrType;
                if (column.IsNullable == "YES" && propertyType.IsValueType)
                    propertyType = typeof(Nullable<>).MakeGenericType(propertyType);

                yield return new DynamicPropertyInfo(column.ColumnName, propertyType, databaseGenerated);
            }
        }
        public String GetTableName(String entityName)
        {
            return entityName;
        }
        public IEnumerable<(String tableEdmName, bool isQueryType)> GetTableNames()
        {
            foreach (var pair in _schemaCache.GetTables())
                yield return (pair.Key, pair.Value.isQueryType);
        }
        public String GetTableSchema(String tableEdmName)
        {
            return _schemaCache.GetTables()[tableEdmName].tableSchema;
        }

        public ICollection<TableMapping> TableMappings {
            get => _schemaCache.TableMappings;
            set => _schemaCache.TableMappings = value;
        }
        public DbContextOptions<Types.DynamicDbContext> DynamicDbContextOptions { get; }
        public bool IsDatabaseNullHighestValue { get; }
    }
}
