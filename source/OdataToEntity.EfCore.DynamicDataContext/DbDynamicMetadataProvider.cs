using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DbDynamicMetadataProvider : DynamicMetadataProvider, IDisposable
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

        private readonly DbContextPool<SchemaContext> _dbContextPool;
        private readonly SchemaCache _schemaCache;

        public DbDynamicMetadataProvider(String connectionString, bool useRelationalNulls)
        {
            DbContextOptions = CreateOptions(connectionString, useRelationalNulls);
            _dbContextPool = new DbContextPool<SchemaContext>(DbContextOptions);
            _schemaCache = new SchemaCache();
        }

        private static DbContextOptions CreateOptions(String connectionString, bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchemaContext>();
            optionsBuilder.UseSqlServer(connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        public void Dispose()
        {
            _dbContextPool.Dispose();
        }
        public override DynamicDependentPropertyInfo GetDependentProperties(String tableName, String navigationPropertyName)
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            try
            {
                (String tableSchema, String tableName, bool isQueryType) tableFullName = _schemaCache.GetTables(schemaContext)[tableName];
                if (_schemaCache.GetNavigations(schemaContext).TryGetValue((tableFullName.tableSchema, tableFullName.tableName), out List<SchemaCache.Navigation> navigations))
                    foreach (SchemaCache.Navigation navigation in navigations)
                        if (navigation.NavigationName == navigationPropertyName)
                        {
                            List<KeyColumnUsage> dependent = _schemaCache.GetKeyColumns(schemaContext)[(navigation.ConstraintSchema, navigation.DependentConstraintName)];
                            List<KeyColumnUsage> principal = _schemaCache.GetKeyColumns(schemaContext)[(navigation.ConstraintSchema, navigation.PrincipalConstraintName)]; ;
                            List<String> principalPropertyNames = principal.OrderBy(p => p.OrdinalPosition).Select(p => p.ColumnName).ToList();
                            List<String> dependentPropertyNames = dependent.OrderBy(p => p.OrdinalPosition).Select(p => p.ColumnName).ToList();
                            return new DynamicDependentPropertyInfo(principal[0].TableName, dependent[0].TableName, principalPropertyNames, dependentPropertyNames, navigation.IsCollection);
                        }
            }
            finally
            {
                _dbContextPool.Return(schemaContext);
            }

            throw new InvalidOperationException("Navigation property " + navigationPropertyName + " not found in table " + tableName);
        }
        public override String GetEntityName(String tableName)
        {
            return tableName;
        }
        public override IEnumerable<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName)
        {
            foreach (NavigationMapping navigationMapping in _schemaCache.GetNavigationMappings(tableEdmName))
                if (!String.IsNullOrEmpty(navigationMapping.ManyToManyTarget))
                    yield return (navigationMapping.NavigationName, navigationMapping.ManyToManyTarget);
        }
        public override IEnumerable<String> GetNavigationProperties(String tableEdmName)
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            try
            {
                (String tableSchema, String tableName, bool isQueryType) tableFullName = _schemaCache.GetTables(schemaContext)[tableEdmName];
                if (_schemaCache.GetNavigations(schemaContext).TryGetValue((tableFullName.tableSchema, tableFullName.tableName), out List<SchemaCache.Navigation> navigations))
                    foreach (SchemaCache.Navigation navigation in navigations)
                        yield return navigation.NavigationName;
            }
            finally
            {
                _dbContextPool.Return(schemaContext);
            }
        }
        public override IEnumerable<String> GetPrimaryKey(String tableEdmName)
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            try
            {
                (String tableSchema, String tableName) tableFullName = _schemaCache.GetTableFullName(tableEdmName);
                String constraintName = _schemaCache.GetPrimaryKeyConstraintNames(schemaContext)[tableFullName];
                List<KeyColumnUsage> keyColumns = _schemaCache.GetKeyColumns(schemaContext)[(tableFullName.tableSchema, constraintName)];
                return keyColumns.OrderBy(c => c.OrdinalPosition).Select(c => c.ColumnName);
            }
            finally
            {
                _dbContextPool.Return(schemaContext);
            }
        }
        public override IEnumerable<DynamicPropertyInfo> GetStructuralProperties(String tableName)
        {
            foreach (DbColumn column in _schemaCache.GetColumns(tableName))
            {
                DatabaseGeneratedOption databaseGenerated;
                if (column.IsIdentity.GetValueOrDefault())
                    databaseGenerated = DatabaseGeneratedOption.Identity;
                else if (column.IsExpression.GetValueOrDefault())
                    databaseGenerated = DatabaseGeneratedOption.Computed;
                else
                    databaseGenerated = DatabaseGeneratedOption.None;

                Type propertyType = column.DataType;
                if (column.AllowDBNull.GetValueOrDefault() && column.DataType.IsValueType)
                    propertyType = typeof(Nullable<>).MakeGenericType(propertyType);

                yield return new DynamicPropertyInfo(column.ColumnName, propertyType, databaseGenerated);
            }
        }
        public override String GetTableName(String entityName)
        {
            return entityName;
        }
        public override IEnumerable<(String tableEdmName, bool isQueryType)> GetTableNames()
        {
            SchemaContext schemaContext = _dbContextPool.Rent();
            try
            {
                foreach (var pair in _schemaCache.GetTables(schemaContext))
                    yield return (pair.Key, pair.Value.isQueryType);
            }
            finally
            {
                _dbContextPool.Return(schemaContext);
            }
        }

        public ICollection<TableMapping> TableMappings
        {
            get => _schemaCache.TableMappings;
            set => _schemaCache.TableMappings = value;
        }
        public override DbContextOptions DbContextOptions { get; }
    }
}
