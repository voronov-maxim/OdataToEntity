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
        private readonly InformationSchemaSettings _informationSchemaSettings;
        private readonly SchemaCache _schemaCache;

        internal DynamicMetadataProvider(ProviderSpecificSchema informationSchema, InformationSchemaSettings informationSchemaSettings)
        {
            InformationSchema = informationSchema;
            _informationSchemaSettings = informationSchemaSettings;

            _schemaCache = SchemaCacheFactory.Create(informationSchema, _informationSchemaSettings);
        }

        public void Dispose()
        {
            _schemaCache.Dispose();
        }
        public DynamicDependentPropertyInfo GetDependentProperties(String tableEdmName, String navigationPropertyName)
        {
            IReadOnlyList<Navigation> navigations = _schemaCache.GetNavigations(tableEdmName);
            if (navigations.Count > 0)
                foreach (Navigation navigation in navigations)
                    if (navigation.NavigationName == navigationPropertyName)
                    {
                        IReadOnlyList<KeyColumnUsage> dependent = _schemaCache.GetKeyColumns(navigation.ConstraintSchema, navigation.DependentConstraintName);
                        IReadOnlyList<KeyColumnUsage> principal = _schemaCache.GetKeyColumns(navigation.ConstraintSchema, navigation.PrincipalConstraintName);
                        var principalPropertyNames = new List<String>(principal.Select(p => p.ColumnName));
                        var dependentPropertyNames = new List<String>(dependent.Select(p => p.ColumnName));

                        String? principalEdmName = _schemaCache.GetTableEdmName(principal[0].TableSchema, principal[0].TableName);
                        if (principalEdmName == null)
                            throw new InvalidOperationException($"Table {principal[0].TableSchema}.{principal[0].TableName} not found");

                        String? dependentEdmName = _schemaCache.GetTableEdmName(dependent[0].TableSchema, dependent[0].TableName);
                        if (dependentEdmName == null)
                            throw new InvalidOperationException($"Table {dependent[0].TableSchema}.{dependent[0].TableName} not found");

                        return new DynamicDependentPropertyInfo(principalEdmName, dependentEdmName, principalPropertyNames, dependentPropertyNames, navigation.IsCollection);
                    }

            throw new InvalidOperationException("Navigation property " + navigationPropertyName + " not found in table " + tableEdmName);
        }
        public IReadOnlyList<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName)
        {
            return _schemaCache.GetManyToManyProperties(tableEdmName);
        }
        public IEnumerable<String> GetNavigationProperties(String tableEdmName)
        {
            foreach (Navigation navigation in _schemaCache.GetNavigations(tableEdmName))
                yield return navigation.NavigationName;
        }
        public (String[] propertyNames, bool isPrimary)[] GetKeys(String tableEdmName)
        {
            IReadOnlyList<(String constraintName, bool isPrimary)> constraints = _schemaCache.GetKeyConstraintNames(tableEdmName);
            if (constraints.Count > 0)
            {
                (String tableSchema, _) = _schemaCache.GetTableFullName(tableEdmName);
                var keys = new (String[] propertyNames, bool isPrimary)[constraints.Count];
                for (int i = 0; i < constraints.Count; i++)
                {
                    IReadOnlyList<KeyColumnUsage> keyColumns = _schemaCache.GetKeyColumns(tableSchema, constraints[i].constraintName);
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
            return _schemaCache.GetRoutines(typeDefinitionManager, _informationSchemaSettings);
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
            return _schemaCache.GetTableEdmNames();
        }
        public String GetTableSchema(String tableEdmName)
        {
            return _schemaCache.GetTableFullName(tableEdmName).tableSchema;
        }

        public ProviderSpecificSchema InformationSchema { get; }
    }
}
