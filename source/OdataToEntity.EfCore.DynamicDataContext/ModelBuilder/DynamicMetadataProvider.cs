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
        private readonly InformationSchemaMapping? _informationSchemaMapping;
        private readonly SchemaCache _schemaCache;

        internal DynamicMetadataProvider(ProviderSpecificSchema informationSchema, InformationSchemaMapping? informationSchemaMapping)
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
                        IReadOnlyList<KeyColumnUsage> dependent = _schemaCache.GetKeyColumns()[(navigation.ConstraintSchema, navigation.DependentConstraintName)];
                        IReadOnlyList<KeyColumnUsage> principal = _schemaCache.GetKeyColumns()[(navigation.ConstraintSchema, navigation.PrincipalConstraintName)]; ;
                        var principalPropertyNames = new List<String>(principal.Select(p => p.ColumnName));
                        var dependentPropertyNames = new List<String>(dependent.Select(p => p.ColumnName));

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
        public (String[] propertyNames, bool isPrimary)[] GetKeys(String tableEdmName)
        {
            (String tableSchema, String tableName) tableFullName = _schemaCache.GetTableFullName(tableEdmName);
            if (_schemaCache.GetKeyConstraintNames().TryGetValue(tableFullName, out IReadOnlyList<(String constraintName, bool isPrimary)> constraints))
            {
                var keys = new (String[] propertyNames, bool isPrimary)[constraints.Count];
                for (int i = 0; i < constraints.Count; i++)
                {
                    IReadOnlyList<KeyColumnUsage> keyColumns = _schemaCache.GetKeyColumns()[(tableFullName.tableSchema, constraints[i].constraintName)];
                    var key = new String[keyColumns.Count];
                    for (int j = 0; j < key.Length; j++)
                        key[j] = keyColumns[j].ColumnName;
                    keys[i] = (key, constraints[i].isPrimary);
                }

                return keys;
            }

            return Array.Empty<(String[] propertyNames, bool isPrimary)>();
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
                bool isNullabe = column.IsNullable == "YES";
                if (isNullabe && propertyType.IsValueType)
                    propertyType = typeof(Nullable<>).MakeGenericType(propertyType);

                yield return new DynamicPropertyInfo(column.ColumnName, propertyType, isNullabe, databaseGenerated);
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
