using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public static class SchemaCacheFactory
    {
        public static SchemaCache Create(ProviderSpecificSchema informationSchema, InformationSchemaSettings informationSchemaSettings)
        {
            IEqualityComparer<String> comparer = informationSchema.IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            IEqualityComparer<TableFullName> tableNameComparer = informationSchema.IsCaseSensitive ? TableFullName.OrdinalComparer : TableFullName.OrdinalIgnoreCaseComparer;
            StringComparison comparison = informationSchema.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var tableEdmNameFullNames = new Dictionary<String, TableFullName>(comparer);
            var tableFullNameEdmNames = new Dictionary<TableFullName, (String tableEdmName, bool isQueryType)>(tableNameComparer);
            var navigationMappings = new Dictionary<TableFullName, IReadOnlyList<NavigationMapping>>(tableNameComparer);
            Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns = GetKeyColumns(informationSchema, informationSchemaSettings);
            List<ReferentialConstraint> referentialConstraints;

            using SchemaContext schemaContext = informationSchema.GetSchemaContext();

            Dictionary<String, TableMapping>? dbNameTableMappings = null;
            if (informationSchemaSettings.Tables != null)
                dbNameTableMappings = informationSchemaSettings.Tables.ToDictionary(t => t.DbName, StringComparer.OrdinalIgnoreCase);

            IQueryable<Table> tableQuery = schemaContext.Tables.AsQueryable();
            IQueryable<ReferentialConstraint> referentialConstraintsQuery = schemaContext.ReferentialConstraints;
            if (informationSchemaSettings.IncludedSchemas != null && informationSchemaSettings.IncludedSchemas.Count > 0)
            {
                tableQuery = tableQuery.Where(t => informationSchemaSettings.IncludedSchemas.Contains(t.TableSchema));
                referentialConstraintsQuery = referentialConstraintsQuery.Where(t => informationSchemaSettings.IncludedSchemas.Contains(t.ConstraintSchema));
            }
            if (informationSchemaSettings.ExcludedSchemas != null && informationSchemaSettings.ExcludedSchemas.Count > 0)
            {
                tableQuery = tableQuery.Where(t => !informationSchemaSettings.ExcludedSchemas.Contains(t.TableSchema));
                referentialConstraintsQuery = referentialConstraintsQuery.Where(t => !informationSchemaSettings.ExcludedSchemas.Contains(t.ConstraintSchema));
            }

            List<Table> tables = tableQuery.ToList();
            referentialConstraints = referentialConstraintsQuery.ToList();

            foreach (Table table in tables)
            {
                String tableName;
                if (informationSchemaSettings.DefaultSchema != null && String.Compare(informationSchemaSettings.DefaultSchema, table.TableName, comparison) == 0)
                    tableName = table.TableName;
                else
                    tableName = table.TableSchema + "." + table.TableName;

                if (dbNameTableMappings != null)
                {
                    if (dbNameTableMappings.TryGetValue(table.TableName, out TableMapping? tableMapping) ||
                        dbNameTableMappings.TryGetValue(table.TableSchema + "." + table.TableName, out tableMapping))
                    {
                        if (tableMapping.Exclude)
                            continue;

                        if (!String.IsNullOrEmpty(tableMapping.EdmName))
                        {
                            tableName = tableMapping.EdmName;
                            if (tableEdmNameFullNames.ContainsKey(tableName))
                                throw new InvalidOperationException("Duplicate TableMapping.EdmName = '" + tableName + "'");
                        }

                        if (tableMapping.Navigations != null && tableMapping.Navigations.Count > 0)
                        {
                            foreach (NavigationMapping navigationMapping in tableMapping.Navigations)
                                if (!String.IsNullOrEmpty(navigationMapping.NavigationName) && String.IsNullOrEmpty(navigationMapping.ConstraintName))
                                {
                                    String? tableName2 = navigationMapping.TargetTableName;
                                    if (tableName2 != null)
                                    {
                                        int i = tableName2.IndexOf('.');
                                        if (i != -1)
                                            tableName2 = tableName2.Substring(i + 1);

                                        navigationMapping.ConstraintName = GetFKeyConstraintName(referentialConstraints, keyColumns, table.TableSchema, table.TableName, tableName2);
                                    }
                                }

                            navigationMappings.Add(new TableFullName(table.TableSchema, table.TableName), tableMapping.Navigations);
                        }
                    }
                    else
                    {
                        if (informationSchemaSettings.ObjectFilter == DbObjectFilter.Mapping)
                            continue;
                    }
                }

                var tableFullName = new TableFullName(table.TableSchema, table.TableName);
                tableEdmNameFullNames.Add(tableName, tableFullName);
                tableFullNameEdmNames.Add(tableFullName, (tableName, table.TableType == "VIEW"));
            }

            Dictionary<TableFullName, List<Column>> tableColumns = GetTableColumns(informationSchema);
            Dictionary<TableFullName, List<Navigation>> tableNavigations = Navigation.GetNavigations(
                referentialConstraints,
                keyColumns,
                tableFullNameEdmNames,
                navigationMappings,
                tableColumns);

            return new SchemaCache(
                informationSchema,
                keyColumns,
                tableFullNameEdmNames,
                tableColumns,
                GetKeyConstraintNames(informationSchema),
                tableNavigations,
                GetManyToManyProperties(navigationMappings, tableEdmNameFullNames));
        }
        private static String? GetFKeyConstraintName(List<ReferentialConstraint> referentialConstraints,
            Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns, String tableSchema, String tableName1, String tableName2)
        {
            foreach (ReferentialConstraint fkey in referentialConstraints)
            {
                KeyColumnUsage dependentKeyColumns = keyColumns[(fkey.ConstraintSchema, fkey.ConstraintName)][0];
                KeyColumnUsage principalKeyColumn = keyColumns[(fkey.UniqueConstraintSchema, fkey.UniqueConstraintName)][0];

                if (String.Compare(dependentKeyColumns.TableSchema, tableSchema, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (String.Compare(dependentKeyColumns.TableName, tableName1, StringComparison.OrdinalIgnoreCase) == 0 &&
                        String.Compare(principalKeyColumn.TableName, tableName2, StringComparison.OrdinalIgnoreCase) == 0)
                        return fkey.ConstraintName;

                    if (String.Compare(dependentKeyColumns.TableName, tableName2, StringComparison.OrdinalIgnoreCase) == 0 &&
                        String.Compare(principalKeyColumn.TableName, tableName1, StringComparison.OrdinalIgnoreCase) == 0)
                        return fkey.ConstraintName;
                }
            }

            return null;
        }
        private static Dictionary<TableFullName, List<(String constraintName, bool isPrimary)>> GetKeyConstraintNames(ProviderSpecificSchema informationSchema)
        {
            var keyConstraintNames = new Dictionary<TableFullName, List<(String constraintName, bool isPrimary)>>();
            using SchemaContext schemaContext = informationSchema.GetSchemaContext();

            var tableConstraints = schemaContext.TableConstraints.AsQueryable()
                .Where(t => (t.ConstraintType == "PRIMARY KEY" || t.ConstraintType == "UNIQUE") && t.TableSchema != null && t.TableName != null)
                .OrderBy(t => t.TableSchema).ThenBy(t => t.TableName).ThenBy(t => t.ConstraintType);

            String? tableSchema = null;
            String? tableName = null;
            List<(String constraintName, bool isPrimary)>? constraints = null;
            foreach (TableConstraint tableConstraint in tableConstraints)
            {
                if (tableSchema != tableConstraint.TableSchema || tableName != tableConstraint.TableName)
                {
                    if (constraints != null)
                        keyConstraintNames.Add(new TableFullName(tableSchema!, tableName!), constraints);

                    tableSchema = tableConstraint.TableSchema;
                    tableName = tableConstraint.TableName;
                    constraints = new List<(String constraintName, bool isPrimary)>();
                }
                constraints!.Add((tableConstraint.ConstraintName, constraints.Count == 0));
            }

            if (constraints != null)
                keyConstraintNames.Add(new TableFullName(tableSchema!, tableName!), constraints);
            return keyConstraintNames;
        }
        private static Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> GetKeyColumns(
            ProviderSpecificSchema informationSchema, InformationSchemaSettings informationSchemaSettings)
        {
            var keyColumns = new Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>>();
            using SchemaContext schemaContext = informationSchema.GetSchemaContext();

            String? constraintSchema = null;
            String? constraintName = null;
            List<KeyColumnUsage>? columns = null;

            IQueryable<KeyColumnUsage> keyColumnQueryable = schemaContext.KeyColumnUsage.AsQueryable();
            if (informationSchemaSettings.IncludedSchemas != null && informationSchemaSettings.IncludedSchemas.Count > 0)
                keyColumnQueryable = keyColumnQueryable.Where(t => informationSchemaSettings.IncludedSchemas.Contains(t.TableSchema));
            if (informationSchemaSettings.ExcludedSchemas != null && informationSchemaSettings.ExcludedSchemas.Count > 0)
                keyColumnQueryable = keyColumnQueryable.Where(t => !informationSchemaSettings.ExcludedSchemas.Contains(t.TableSchema));
            foreach (KeyColumnUsage keyColumn in keyColumnQueryable.OrderBy(t => t.TableSchema).ThenBy(t => t.TableName).ThenBy(t => t.ConstraintName).ThenBy(t => t.OrdinalPosition))
            {
                if (constraintSchema != keyColumn.ConstraintSchema || constraintName != keyColumn.ConstraintName)
                {
                    if (columns != null)
                        keyColumns.Add((constraintSchema!, constraintName!), columns);

                    constraintSchema = keyColumn.ConstraintSchema;
                    constraintName = keyColumn.ConstraintName;
                    columns = new List<KeyColumnUsage>();
                }
                columns!.Add(keyColumn);
            }

            if (columns != null)
                keyColumns.Add((constraintSchema!, constraintName!), columns);

            return keyColumns;
        }
        private static Dictionary<TableFullName, List<(String NavigationName, TableFullName ManyToManyTarget)>> GetManyToManyProperties(
            Dictionary<TableFullName, IReadOnlyList<NavigationMapping>> navigationMappings,
            Dictionary<String, TableFullName> tableEdmNameFullNames)
        {
            var manyToManyProperties = new Dictionary<TableFullName, List<(String NavigationName, TableFullName ManyToManyTarget)>>();
            foreach (KeyValuePair<TableFullName, IReadOnlyList<NavigationMapping>> pair in navigationMappings)
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    NavigationMapping navigationMapping = pair.Value[i];
                    if (!String.IsNullOrEmpty(navigationMapping.ManyToManyTarget))
                    {
                        if (!manyToManyProperties.TryGetValue(pair.Key, out List<(String NavigationName, TableFullName ManyToManyTarget)>? manyToManies))
                        {
                            manyToManies = new List<(String NavigationName, TableFullName ManyToManyTarget)>();
                            manyToManyProperties.Add(pair.Key, manyToManies);
                        }

                        if (navigationMapping.NavigationName == null)
                            throw new InvalidOperationException("For ManyToManyTarget" + navigationMapping.ManyToManyTarget + " NavigationName must be not null");

                        TableFullName manyToManyTarget = tableEdmNameFullNames[navigationMapping.ManyToManyTarget];
                        manyToManies.Add((navigationMapping.NavigationName, manyToManyTarget));
                    }
                }

            return manyToManyProperties;
        }
        private static Dictionary<TableFullName, List<Column>> GetTableColumns(ProviderSpecificSchema informationSchema)
        {
            var tableColumns = new Dictionary<TableFullName, List<Column>>();
            using SchemaContext schemaContext = informationSchema.GetSchemaContext();

            var dbGeneratedColumns = informationSchema.GetDbGeneratedColumns().ToDictionary(t => (t.TableSchema, t.TableName, t.ColumnName));
            foreach (Column column in schemaContext.Columns)
            {
                var tableFullName = new TableFullName(column.TableSchema, column.TableName);
                if (!tableColumns.TryGetValue(tableFullName, out List<Column>? columns))
                {
                    columns = new List<Column>();
                    tableColumns.Add(tableFullName, columns);
                }

                Type? clrType = informationSchema.GetColumnClrType(column.DataType);
                if (clrType == null)
                    continue;

                column.ClrType = clrType;
                if (dbGeneratedColumns.TryGetValue((column.TableSchema, column.TableName, column.ColumnName), out DbGeneratedColumn? dbGeneratedColumn))
                {
                    column.IsComputed = dbGeneratedColumn.IsComputed;
                    column.IsIdentity = dbGeneratedColumn.IsIdentity;
                }

                columns.Add(column);
            }

            return tableColumns;
        }
    }
}
