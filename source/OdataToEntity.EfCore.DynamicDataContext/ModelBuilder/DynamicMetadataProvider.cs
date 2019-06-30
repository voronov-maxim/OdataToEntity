using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.ModelBuilder
{
    public sealed class DynamicMetadataProvider : IDisposable
    {
        private readonly InformationSchemaMapping _informationSchemaMapping;
        private readonly SchemaCache _schemaCache;

        internal DynamicMetadataProvider(ProviderSpecificSchema informationSchema, InformationSchemaMapping informationSchemaMapping)
        {
            InformationSchema = informationSchema;
            _informationSchemaMapping = informationSchemaMapping;

            _schemaCache = new SchemaCache(informationSchema);
            _schemaCache.Initialize(_informationSchemaMapping?.Tables);
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

                        String principalEdmName = _schemaCache.GetTableEdmName(principal[0].TableSchema, principal[0].TableName);
                        String dependentEdmName = _schemaCache.GetTableEdmName(dependent[0].TableSchema, dependent[0].TableName);
                        return new DynamicDependentPropertyInfo(principalEdmName, dependentEdmName, principalPropertyNames, dependentPropertyNames, navigation.IsCollection);
                    }

            throw new InvalidOperationException("Navigation property " + navigationPropertyName + " not found in table " + tableName);
        }
        public IReadOnlyList<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName)
        {
            var manyToManyProperties = _schemaCache.GetManyToManyProperties();
            if (manyToManyProperties.TryGetValue(tableEdmName, out IReadOnlyList<(String NavigationName, String ManyToManyTarget)> tableManyToManyProperties))
                return tableManyToManyProperties;

            return Array.Empty<(String NavigationName, String ManyToManyTarget)>();
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
        public IReadOnlyList<OeOperationConfiguration> GetRoutines(DynamicTypeDefinitionManager typeDefinitionManager)
        {
            return _schemaCache.GetRoutines(typeDefinitionManager, _informationSchemaMapping);
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
        public String GetTableEdmName(String entityName)
        {
            return entityName;
        }
        public IEnumerable<(String tableEdmName, bool isQueryType)> GetTableEdmNames()
        {
            foreach (var pair in _schemaCache.GetTables())
                yield return (pair.Key, pair.Value.isQueryType);
        }
        public String GetTableSchema(String tableEdmName)
        {
            return _schemaCache.GetTables()[tableEdmName].tableSchema;
        }

        public ProviderSpecificSchema InformationSchema { get; }
    }
}
