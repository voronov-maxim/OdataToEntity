using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public static class SchemaCacheFactory
    {
        private sealed class TupleStringComparer : IEqualityComparer<(String, String)>
        {
            private readonly StringComparer _stringComparer;
            public static readonly TupleStringComparer Ordinal = new TupleStringComparer(StringComparer.Ordinal);
            public static readonly TupleStringComparer OrdinalIgnoreCase = new TupleStringComparer(StringComparer.OrdinalIgnoreCase);

            private TupleStringComparer(StringComparer stringComparer)
            {
                _stringComparer = stringComparer;
            }

            public bool Equals((String, String) x, (String, String) y)
            {
                return _stringComparer.Compare(x.Item1, y.Item1) == 0 && _stringComparer.Compare(x.Item2, y.Item2) == 0;
            }
            public int GetHashCode((String, String) obj)
            {
                int h1 = _stringComparer.GetHashCode(obj.Item1);
                int h2 = _stringComparer.GetHashCode(obj.Item2);
                return (h1 << 5) + h1 ^ h2;
            }
        }

        public static SchemaCache Create(ProviderSpecificSchema informationSchema, InformationSchemaSettings informationSchemaSettings)
        {
            IEqualityComparer<String> comparer = informationSchema.IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            TupleStringComparer tupleComparer = informationSchema.IsCaseSensitive ? TupleStringComparer.Ordinal : TupleStringComparer.OrdinalIgnoreCase;

            var tableEdmNameFullNames = new Dictionary<String, (String tableSchema, String tableName, bool isQueryType)>(comparer);
            var tableFullNameEdmNames = new Dictionary<(String tableSchema, String tableName), String>(tupleComparer);
            var navigationMappings = new Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>>(tupleComparer);
            Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns = GetKeyColumns(informationSchema, informationSchemaSettings);
            List<ReferentialConstraint> referentialConstraints;

            SchemaContext schemaContext = informationSchema.GetSchemaContext();
            try
            {
                Dictionary<String, TableMapping>? dbNameTableMappings = null;
                if (informationSchemaSettings.Tables != null)
                    dbNameTableMappings = informationSchemaSettings.Tables.ToDictionary(t => t.DbName, StringComparer.OrdinalIgnoreCase);

                var fixTableNames = new List<Table>();

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
                    String tableName = table.TableName;
                    if (tableEdmNameFullNames.ContainsKey(tableName))
                    {
                        fixTableNames.Add(table);
                        tableName = table.TableSchema + "." + table.TableName;
                    }

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

                                navigationMappings.Add((table.TableSchema, table.TableName), tableMapping.Navigations);
                            }
                        }
                        else
                        {
                            if (informationSchemaSettings.ObjectFilter == DbObjectFilter.Mapping)
                                continue;
                        }
                    }

                    tableEdmNameFullNames.Add(tableName, (table.TableSchema, table.TableName, table.TableType == "VIEW"));
                    tableFullNameEdmNames.Add((table.TableSchema, table.TableName), tableName);
                }

                foreach (Table fixTableName in fixTableNames)
                    tableEdmNameFullNames[fixTableName.TableSchema + "." + fixTableName.TableName] = (fixTableName.TableSchema, fixTableName.TableName, fixTableName.TableType == "VIEW");
            }
            finally
            {
                schemaContext.Dispose();
            }

            Dictionary<(String tableSchema, String tableName), List<Column>> tableColumns = GetTableColumns(informationSchema);
            Dictionary<(String tableSchema, String tableName), List<(String constraintName, bool isPrimary)>> keyConstraintNames = GetKeyConstraintNames(informationSchema);
            Dictionary<(String, String), List<Navigation>> tableNavigations = Navigation.GetNavigations(
                referentialConstraints,
                keyColumns,
                tableFullNameEdmNames,
                navigationMappings,
                tableColumns);

            return new SchemaCache(
                informationSchema,
                keyColumns,
                tableEdmNameFullNames,
                tableFullNameEdmNames,
                navigationMappings,
                tableColumns,
                keyConstraintNames,
                tableNavigations);
        }
        private static Dictionary<(String tableSchema, String tableName), List<(String constraintName, bool isPrimary)>> GetKeyConstraintNames(ProviderSpecificSchema informationSchema)
        {
            var keyConstraintNames = new Dictionary<(String tableSchema, String tableName), List<(String constraintName, bool isPrimary)>>();
            SchemaContext schemaContext = informationSchema.GetSchemaContext();
            try
            {
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
                            keyConstraintNames.Add((tableSchema!, tableName!), constraints);

                        tableSchema = tableConstraint.TableSchema;
                        tableName = tableConstraint.TableName;
                        constraints = new List<(String constraintName, bool isPrimary)>();
                    }
                    constraints!.Add((tableConstraint.ConstraintName, constraints.Count == 0));
                }

                if (constraints != null)
                    keyConstraintNames.Add((tableSchema!, tableName!), constraints);
            }
            finally
            {
                schemaContext.Dispose();
            }
            return keyConstraintNames;
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
        private static Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> GetKeyColumns(
            ProviderSpecificSchema informationSchema, InformationSchemaSettings informationSchemaSettings)
        {
            var keyColumns = new Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>>();
            SchemaContext schemaContext = informationSchema.GetSchemaContext();
            try
            {
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
            }
            finally
            {
                schemaContext.Dispose();
            }

            return keyColumns;
        }
        private static Dictionary<(String tableSchema, String tableName), List<Column>> GetTableColumns(ProviderSpecificSchema informationSchema)
        {
            var tableColumns = new Dictionary<(String tableSchema, String tableName), List<Column>>();
            SchemaContext schemaContext = informationSchema.GetSchemaContext();
            var dbGeneratedColumns = informationSchema.GetDbGeneratedColumns().ToDictionary(t => (t.TableSchema, t.TableName, t.ColumnName));
            try
            {
                foreach (Column column in schemaContext.Columns)
                {
                    if (!tableColumns.TryGetValue((column.TableSchema, column.TableName), out List<Column>? columns))
                    {
                        columns = new List<Column>();
                        tableColumns.Add((column.TableSchema, column.TableName), columns);
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
            }
            finally
            {
                schemaContext.Dispose();
            }

            return tableColumns;
        }
    }
}
