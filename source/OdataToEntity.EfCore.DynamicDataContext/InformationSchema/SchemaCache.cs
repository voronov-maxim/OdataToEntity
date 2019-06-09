using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SchemaCache : IDisposable
    {
        public readonly struct Navigation
        {
            public Navigation(String constraintSchema, String dependentConstraintName, String principalConstraintName, String navigationName, bool isCollection)
            {
                ConstraintSchema = constraintSchema;
                DependentConstraintName = dependentConstraintName;
                PrincipalConstraintName = principalConstraintName;
                NavigationName = navigationName;
                IsCollection = isCollection;
            }

            public String ConstraintSchema { get; }
            public String DependentConstraintName { get; }
            public String NavigationName { get; }
            public bool IsCollection { get; }
            public String PrincipalConstraintName { get; }
        }

        private Dictionary<(String constraintSchema, String constraintName), List<KeyColumnUsage>> _keyColumns;
        private Dictionary<(String tableSchema, String tableName), ICollection<NavigationMapping>> _navigationMappings;
        private Dictionary<(String tableSchema, String tableName), String> _primaryKeys;
        private Dictionary<(String tableSchema, String tableName), List<Column>> _tableColumns;
        private Dictionary<(String tableSchema, String tableName), List<Navigation>> _tableNavigations;
        private Dictionary<String, (String tableSchema, String tableName, bool isQueryType)> _tableEdmNameFullNames;
        private Dictionary<(String tableSchema, String tableName), String> _tableFullNameEdmNames;

        private readonly ProviderSpecificSchema _informationSchema;

        public SchemaCache(ProviderSpecificSchema informationSchema)
        {
            _informationSchema = informationSchema;
        }

        private void AddNavigation(ReferentialConstraint fkey, KeyColumnUsage keyColumn, String navigationName, bool isCollection)
        {
            if (_navigationMappings.TryGetValue((keyColumn.TableSchema, keyColumn.TableName), out ICollection<NavigationMapping> navigationMappings))
                foreach (NavigationMapping navigationMapping in navigationMappings)
                    if (String.CompareOrdinal(navigationMapping.ConstraintName, fkey.ConstraintName) == 0)
                    {
                        navigationName = navigationMapping.NavigationName;
                        break;
                    }

            if (!String.IsNullOrEmpty(navigationName))
            {
                (String tableName, String tableSchema) tableFullName = (keyColumn.TableSchema, keyColumn.TableName);
                if (!_tableNavigations.TryGetValue(tableFullName, out List<Navigation> principalNavigations))
                {
                    principalNavigations = new List<Navigation>();
                    _tableNavigations.Add(tableFullName, principalNavigations);
                }
                principalNavigations.Add(new Navigation(fkey.ConstraintSchema, fkey.ConstraintName, fkey.UniqueConstraintName, navigationName, isCollection));
            }
        }
        public void Dispose()
        {
            _informationSchema.Dispose();
        }
        public IReadOnlyList<Column> GetColumns(String tableEdmName)
        {
            if (!_tableEdmNameFullNames.TryGetValue(tableEdmName, out (String tableSchema, String tableName, bool isQueryType) tableFullName))
                return null;

            return GetColumns(tableFullName.tableSchema, tableFullName.tableName);
        }
        public IReadOnlyList<Column> GetColumns(String tableSchema, String tableName)
        {
            if (_tableColumns == null)
            {
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                var tableColumns = new Dictionary<(String tableSchema, String tableName), List<Column>>();
                try
                {
                    foreach (Column column in schemaContext.Columns)
                    {
                        if (!tableColumns.TryGetValue((column.TableSchema, column.TableName), out List<Column> columns))
                        {
                            columns = new List<Column>();
                            tableColumns.Add((column.TableSchema, column.TableName), columns);
                        }

                        column.ClrType = _informationSchema.GetColumnClrType(column.DataType);
                        var dbGeneratedColumns = _informationSchema.GetDbGeneratedColumns().ToDictionary(t => (t.TableSchema, t.TableName, t.ColumnName));
                        if (dbGeneratedColumns.TryGetValue((column.TableSchema, column.TableName, column.ColumnName), out DbGeneratedColumn dbGeneratedColumn))
                        {
                            column.IsComputed = dbGeneratedColumn.IsComputed;
                            column.IsIdentity = dbGeneratedColumn.IsIdentity;
                        }

                        columns.Add(column);
                    }
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }

                _tableColumns = tableColumns;
            }

            return _tableColumns[(tableSchema, tableName)];
        }
        public Dictionary<(String constraintSchema, String constraintName), List<KeyColumnUsage>> GetKeyColumns()
        {
            if (_keyColumns == null)
            {
                var keyColumns = new Dictionary<(String constraintSchema, String constraintName), List<KeyColumnUsage>>();
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    foreach (KeyColumnUsage keyColumn in schemaContext.KeyColumnUsage)
                    {
                        var key = (keyColumn.ConstraintSchema, keyColumn.ConstraintName);
                        if (!keyColumns.TryGetValue(key, out List<KeyColumnUsage> columns))
                        {
                            columns = new List<KeyColumnUsage>();
                            keyColumns.Add(key, columns);
                        }
                        columns.Add(keyColumn);
                    }
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }

                _keyColumns = keyColumns;
            }
            return _keyColumns;
        }
        public ICollection<NavigationMapping> GetNavigationMappings(String tableEdmName)
        {
            if (_tableEdmNameFullNames.TryGetValue(tableEdmName, out (String tableSchema, String tableName, bool isQueryType) tableFullName))
                if (_navigationMappings.TryGetValue((tableFullName.tableSchema, tableFullName.tableName), out ICollection<NavigationMapping> _navigationMapping))
                    return _navigationMapping;

            return Array.Empty<NavigationMapping>();
        }
        public Dictionary<(String, String), List<Navigation>> GetNavigations()
        {
            if (_tableNavigations == null)
            {
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    _tableNavigations = new Dictionary<(String tableSchema, String tableName), List<Navigation>>();
                    var keyColumns = GetKeyColumns();

                    var navigationCounter = new Dictionary<(String, String, String), int>();
                    foreach (ReferentialConstraint fkey in schemaContext.ReferentialConstraints)
                    {
                        KeyColumnUsage dependentKeyColumn = keyColumns[(fkey.ConstraintSchema, fkey.ConstraintName)][0];
                        if (GetTableEdmName(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName) == null)
                            continue;

                        KeyColumnUsage principalKeyColumn = keyColumns[(fkey.UniqueConstraintSchema, fkey.UniqueConstraintName)][0];
                        if (GetTableEdmName(principalKeyColumn.TableSchema, principalKeyColumn.TableName) == null)
                            continue;

                        (String, String, String) dependentPrincipalKey = (fkey.ConstraintSchema, dependentKeyColumn.TableName, principalKeyColumn.TableName);
                        if (navigationCounter.TryGetValue(dependentPrincipalKey, out int counter))
                            counter++;
                        else
                            counter = 1;
                        navigationCounter[dependentPrincipalKey] = counter;

                        String dependentNavigationName = principalKeyColumn.TableName;
                        String principalNavigationName = dependentKeyColumn.TableName;
                        if (dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName)
                        {
                            dependentNavigationName = "Parent";
                            principalNavigationName = "Children";
                        }

                        IReadOnlyList<Column> dependentColumns = GetColumns(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName);
                        counter = GetCount(dependentColumns, dependentNavigationName, counter);

                        IReadOnlyList<Column> principalColumns = GetColumns(principalKeyColumn.TableSchema, principalKeyColumn.TableName);
                        counter = GetCount(principalColumns, principalNavigationName, counter);

                        if (counter > 1)
                        {
                            String scounter = counter.ToString(CultureInfo.InvariantCulture);
                            dependentNavigationName += scounter;
                            principalNavigationName += scounter;
                        }

                        AddNavigation(fkey, dependentKeyColumn, dependentNavigationName, false);
                        AddNavigation(fkey, principalKeyColumn, principalNavigationName, true);
                    }
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }
            }
            return _tableNavigations;

            int GetCount(IReadOnlyList<Column> columns, String navigationName, int counter)
            {
                bool match;
                do
                {
                    match = false;
                    String scounter = counter == 1 ? "" : counter.ToString(CultureInfo.InvariantCulture);
                    for (int i = 0; i < columns.Count && !match; i++)
                    {
                        String columnName = columns[i].ColumnName;
                        if (columnName.StartsWith(navigationName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            int length = columnName.Length - navigationName.Length;
                            if (String.Compare(columnName, navigationName.Length, scounter, 0, length, StringComparison.InvariantCultureIgnoreCase) == 0)
                                match = scounter.Length == length;
                        }
                    }

                    if (match)
                        counter++;
                }
                while (match);

                return counter;
            }
        }
        public Dictionary<(String tableSchema, String tableName), String> GetPrimaryKeyConstraintNames()
        {
            if (_primaryKeys == null)
            {
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    _primaryKeys = schemaContext.TableConstraints.Where(t => t.ConstraintType == "PRIMARY KEY").ToDictionary(t => (t.TableSchema, t.TableName), t => t.ConstraintName);
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }
            }
            return _primaryKeys;
        }
        public String GetTableEdmName(String tableSchema, String tableName)
        {
            _tableFullNameEdmNames.TryGetValue((tableSchema, tableName), out String tableEdmName);
            return tableEdmName;
        }
        public (String tableSchema, String tableName) GetTableFullName(String tableEdmName)
        {
            (String tableSchema, String tableName, bool _) = _tableEdmNameFullNames[tableEdmName];
            return (tableSchema, tableName);
        }
        public Dictionary<String, (String tableSchema, String tableName, bool isQueryType)> GetTables()
        {
            if (_tableEdmNameFullNames == null)
            {
                var navigationMappings = new Dictionary<(String, String), ICollection<NavigationMapping>>();
                var tableEdmNameFullNames = new Dictionary<String, (String tableSchema, String tableName, bool isQueryType)>(StringComparer.InvariantCultureIgnoreCase);
                var tableFullNameEdmNames = new Dictionary<(String tableSchema, String tableName), String>();

                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    Dictionary<String, TableMapping> tableMappings = null;
                    if (TableMappings != null)
                        tableMappings = TableMappings.ToDictionary(t => t.DbName, StringComparer.InvariantCultureIgnoreCase);

                    var fixTableNames = new List<String>();
                    List<Table> tables = schemaContext.Tables.ToList();
                    foreach (Table table in tables)
                    {
                        String tableName = table.TableName;
                        if (tableEdmNameFullNames.ContainsKey(tableName))
                        {
                            fixTableNames.Add(tableName);
                            tableName = table.TableSchema + table.TableName;
                        }

                        if (tableMappings != null)
                        {
                            if (tableMappings.TryGetValue(table.TableName, out TableMapping tableMapping) ||
                            tableMappings.TryGetValue(table.TableSchema + "." + table.TableName, out tableMapping))
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
                                    navigationMappings.Add((table.TableSchema, table.TableName), tableMapping.Navigations);
                            }
                            else
                                continue;
                        }

                        tableEdmNameFullNames.Add(tableName, (table.TableSchema, table.TableName, table.TableType == "VIEW"));
                        tableFullNameEdmNames.Add((table.TableSchema, table.TableName), tableName);
                    }

                    foreach (String tableName in fixTableNames)
                    {
                        int index = tables.FindIndex(t => t.TableName == tableName);
                        tableEdmNameFullNames[tables[index].TableSchema + tables[index].TableName] = (tables[index].TableSchema, tables[index].TableName, tables[index].TableType == "VIEW");
                    }
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }

                _navigationMappings = navigationMappings;
                _tableFullNameEdmNames = tableFullNameEdmNames;
                _tableEdmNameFullNames = tableEdmNameFullNames;
            }
            return _tableEdmNameFullNames;
        }

        public ICollection<TableMapping> TableMappings { get; set; }
    }
}
