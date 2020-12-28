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
        public DynamicDependentPropertyInfo GetDependentProperties(in TableFullName tableFullName, String navigationPropertyName)
        {
            IReadOnlyList<Navigation> navigations = _schemaCache.GetNavigations(tableFullName);
            if (navigations.Count > 0)
                foreach (Navigation navigation in navigations)
                    if (navigation.NavigationName == navigationPropertyName)
                    {
                        IReadOnlyList<KeyColumnUsage> dependent = _schemaCache.GetKeyColumns(navigation.ConstraintSchema, navigation.DependentConstraintName);
                        IReadOnlyList<KeyColumnUsage> principal = _schemaCache.GetKeyColumns(navigation.ConstraintSchema, navigation.PrincipalConstraintName);
                        var principalPropertyNames = new List<String>(principal.Select(p => p.ColumnName));
                        var dependentPropertyNames = new List<String>(dependent.Select(p => p.ColumnName));

                        var principalFullName = new TableFullName(principal[0].TableSchema, principal[0].TableName);
                        var dependentFullName = new TableFullName(dependent[0].TableSchema, dependent[0].TableName);
                        return new DynamicDependentPropertyInfo(principalFullName, dependentFullName, principalPropertyNames, dependentPropertyNames, navigation.IsCollection);
                    }

            throw new InvalidOperationException("Navigation property " + navigationPropertyName + " not found in table " + tableFullName);
        }
        public IReadOnlyList<(String NavigationName, TableFullName ManyToManyTarget)> GetManyToManyProperties(in TableFullName tableFullName)
        {
            return _schemaCache.GetManyToManyProperties(tableFullName);
        }
        public IEnumerable<String> GetNavigationProperties(TableFullName tableFullName)
        {
            foreach (Navigation navigation in _schemaCache.GetNavigations(tableFullName))
                yield return navigation.NavigationName;
        }
        public (String[] propertyNames, bool isPrimary)[] GetKeys(in TableFullName tableFullName)
        {
            IReadOnlyList<(String constraintName, bool isPrimary)> constraints = _schemaCache.GetKeyConstraintNames(tableFullName);
            if (constraints.Count > 0)
            {
                var keys = new (String[] propertyNames, bool isPrimary)[constraints.Count];
                for (int i = 0; i < constraints.Count; i++)
                {
                    IReadOnlyList<KeyColumnUsage> keyColumns = _schemaCache.GetKeyColumns(tableFullName.Schema, constraints[i].constraintName);
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
        public IEnumerable<DynamicPropertyInfo> GetStructuralProperties(TableFullName tableFullName)
        {
            foreach (Column column in _schemaCache.GetColumns(tableFullName))
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
        public String GetTableEdmName(in TableFullName tableFullName)
        {
            return _schemaCache.GetTableEdmName(tableFullName);
        }
        public IEnumerable<(TableFullName tableFullName, bool isQueryType)> GetTableFullNames()//zzz
        {
            return _schemaCache.GetTableFullNames();
        }

        public ProviderSpecificSchema InformationSchema { get; }
    }
}
