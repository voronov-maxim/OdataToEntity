using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Globalization;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SchemaCache
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

        private readonly Dictionary<(String, String), ReadOnlyCollection<DbColumn>> _columns;
        private Dictionary<(String, String), List<KeyColumnUsage>> _keyColumns;
        private Dictionary<(String, String), ICollection<NavigationMapping>> _navigationMappings;
        private Dictionary<(String, String), List<Navigation>> _tableNavigations;
        private Dictionary<String, (String, String)> _tableEdmNameFullNames;
        private Dictionary<(String, String), String> _tableFullNameEdmNames;

        public SchemaCache()
        {
            _columns = new Dictionary<(String, String), ReadOnlyCollection<DbColumn>>();
        }

        private void AddNavigation(Dictionary<(String, String), List<Navigation>> tableNavigations, ReferentialConstraint fkey, KeyColumnUsage keyColumn, String navigationName, bool isCollection)
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
                (String, String) tableFullName = (keyColumn.TableSchema, keyColumn.TableName);
                if (!tableNavigations.TryGetValue(tableFullName, out List<Navigation> principalNavigations))
                {
                    principalNavigations = new List<Navigation>();
                    tableNavigations.Add(tableFullName, principalNavigations);
                }
                principalNavigations.Add(new Navigation(fkey.ConstraintSchema, fkey.ConstraintName, fkey.UniqueConstraintName, navigationName, isCollection));
            }
        }
        public ReadOnlyCollection<DbColumn> GetColumns(String tableName)
        {
            if (!_tableEdmNameFullNames.TryGetValue(tableName, out (String, String) tableFullName))
                return null;

            return GetColumns(tableFullName.Item1, tableFullName.Item2);
        }
        public ReadOnlyCollection<DbColumn> GetColumns(String tableSchema, String tableName)
        {
            return _columns[(tableSchema, tableName)];
        }
        public Dictionary<(String, String), List<KeyColumnUsage>> GetKeyColumns(SchemaContext schemaContext)
        {
            if (_keyColumns == null)
            {
                var keyColumns = new Dictionary<(String, String), List<KeyColumnUsage>>();
                foreach (KeyColumnUsage keyColumn in schemaContext.KeyColumnUsage)
                {
                    var key = ValueTuple.Create(keyColumn.ConstraintSchema, keyColumn.ConstraintName);
                    if (!keyColumns.TryGetValue(key, out List<KeyColumnUsage> columns))
                    {
                        columns = new List<KeyColumnUsage>();
                        keyColumns.Add(key, columns);
                    }
                    columns.Add(keyColumn);
                }

                _keyColumns = keyColumns;
            }
            return _keyColumns;
        }
        public ICollection<NavigationMapping> GetNavigationMappings(String tableEdmName)
        {
            if (_tableEdmNameFullNames.TryGetValue(tableEdmName, out (String, String) tableFullName))
                if (_navigationMappings.TryGetValue((tableFullName.Item1, tableFullName.Item2), out ICollection<NavigationMapping> _navigationMapping))
                    return _navigationMapping;

            return Array.Empty<NavigationMapping>();
        }
        public Dictionary<(String, String), List<Navigation>> GetNavigations(SchemaContext schemaContext)
        {
            if (_tableNavigations == null)
            {
                var keyColumns = GetKeyColumns(schemaContext);

                var navigationCounter = new Dictionary<(String, String, String), int>();
                var tableNavigations = new Dictionary<(String, String), List<Navigation>>();
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

                    ReadOnlyCollection<DbColumn> dependentColumns = GetColumns(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName);
                    counter = GetCount(dependentColumns, dependentNavigationName, counter);

                    ReadOnlyCollection<DbColumn> principalColumns = GetColumns(principalKeyColumn.TableSchema, principalKeyColumn.TableName);
                    counter = GetCount(principalColumns, principalNavigationName, counter);

                    if (counter > 1)
                    {
                        String scounter = counter.ToString(CultureInfo.InvariantCulture);
                        dependentNavigationName += scounter;
                        principalNavigationName += scounter;
                    }

                    AddNavigation(tableNavigations, fkey, dependentKeyColumn, dependentNavigationName, false);
                    AddNavigation(tableNavigations, fkey, principalKeyColumn, principalNavigationName, true);
                }

                _tableNavigations = tableNavigations;
            }
            return _tableNavigations;

            int GetCount(ReadOnlyCollection<DbColumn> columns, String navigationName, int counter)
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
        public String GetTableEdmName(String tableSchema, String tableName)
        {
            _tableFullNameEdmNames.TryGetValue((tableSchema, tableName), out String tableEdmName);
            return tableEdmName;
        }
        public Dictionary<String, (String, String)> GetTables(SchemaContext schemaContext)
        {
            if (_tableEdmNameFullNames == null)
            {
                Dictionary<String, TableMapping> tableMappings = null;
                if (TableMappings != null)
                    tableMappings = TableMappings.ToDictionary(t => t.DbName, StringComparer.InvariantCultureIgnoreCase);

                var navigationMappings = new Dictionary<(String, String), ICollection<NavigationMapping>>();

                var tableEdmNameFullNames = new Dictionary<String, (String, String)>(StringComparer.InvariantCultureIgnoreCase);
                var tableFullNameEdmNames = new Dictionary<(String, String), String>();

                var fixTableNames = new List<String>();
                List<Table> tables = schemaContext.Tables.Where(t => t.TableType == "BASE TABLE").ToList();
                foreach (Table table in tables)
                {
                    String tableName = table.TableName;
                    if (tableEdmNameFullNames.ContainsKey(tableName))
                    {
                        fixTableNames.Add(tableName);
                        tableName = table.TableSchema + table.TableName;
                    }

                    (String, String) tableFullName = (table.TableSchema, table.TableName);
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
                                navigationMappings.Add(tableFullName, tableMapping.Navigations);
                        }
                        else
                            continue;
                    }

                    tableEdmNameFullNames.Add(tableName, tableFullName);
                    tableFullNameEdmNames.Add(tableFullName, tableName);

                    ReadOnlyCollection<DbColumn> columns = schemaContext.GetColumns(table.TableSchema, table.TableName);
                    _columns.Add((table.TableSchema, table.TableName), columns);
                }

                foreach (String tableName in fixTableNames)
                {
                    int index = tables.FindIndex(t => t.TableName == tableName);
                    tableEdmNameFullNames[tables[index].TableSchema + tables[index].TableName] = (tables[index].TableSchema, tables[index].TableName);
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
