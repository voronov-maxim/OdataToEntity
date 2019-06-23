using OdataToEntity.ModelBuilder;
using Pluralize.NET;
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
        private Dictionary<String, IReadOnlyList<(String NavigationName, String ManyToManyTarget)>> _manyToManyProperties;
        private Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> _navigationMappings;
        private Dictionary<(String tableSchema, String tableName), String> _primaryKeys;
        private List<OeOperationConfiguration> _routines;
        private Dictionary<(String tableSchema, String tableName), List<Column>> _tableColumns;
        private Dictionary<(String tableSchema, String tableName), List<Navigation>> _tableNavigations;
        private Dictionary<String, (String tableSchema, String tableName, bool isQueryType)> _tableEdmNameFullNames;
        private Dictionary<(String tableSchema, String tableName), String> _tableFullNameEdmNames;

        private readonly ProviderSpecificSchema _informationSchema;
        private readonly Pluralizer _pluralizer;

        public SchemaCache(ProviderSpecificSchema informationSchema)
        {
            _informationSchema = informationSchema;
            _pluralizer = new Pluralizer();
        }

        private void AddNavigation(ReferentialConstraint fkey, KeyColumnUsage keyColumn, String navigationName, bool isCollection)
        {
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
        public IReadOnlyDictionary<(String constraintSchema, String constraintName), List<KeyColumnUsage>> GetKeyColumns()
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
        public IReadOnlyDictionary<String, IReadOnlyList<(String NavigationName, String ManyToManyTarget)>> GetManyToManyProperties()
        {
            if (_manyToManyProperties == null)
            {
                _manyToManyProperties = new Dictionary<String, IReadOnlyList<(String NavigationName, String ManyToManyTarget)>>();
                foreach (KeyValuePair<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> pair in _navigationMappings)
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        NavigationMapping navigationMapping = pair.Value[i];
                        if (!String.IsNullOrEmpty(navigationMapping.ManyToManyTarget))
                        {
                            String tableEdmName = _tableFullNameEdmNames[pair.Key];
                            List<(String NavigationName, String ManyToManyTarget)> manyToManies;
                            if (_manyToManyProperties.TryGetValue(tableEdmName, out IReadOnlyList<(String NavigationName, String ManyToManyTarget)> list))
                                manyToManies = (List<(String NavigationName, String ManyToManyTarget)>)list;
                            else
                            {
                                manyToManies = new List<(String NavigationName, String ManyToManyTarget)>();
                                _manyToManyProperties.Add(tableEdmName, manyToManies);
                            }
                            manyToManies.Add((navigationMapping.NavigationName, navigationMapping.ManyToManyTarget));
                        }
                    }
            }

            return _manyToManyProperties;
        }
        private String GetNavigationMappingName(ReferentialConstraint fkey, KeyColumnUsage keyColumn)
        {
            if (_navigationMappings.TryGetValue((keyColumn.TableSchema, keyColumn.TableName), out IReadOnlyList<NavigationMapping> navigationMappings))
                for (int i = 0; i < navigationMappings.Count; i++)
                {
                    NavigationMapping navigationMapping = navigationMappings[i];
                    if (String.CompareOrdinal(navigationMapping.ConstraintName, fkey.ConstraintName) == 0)
                        return navigationMapping.NavigationName;
                }

            return null;
        }
        public IReadOnlyDictionary<(String, String), List<Navigation>> GetNavigations()
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

                        String dependentNavigationName = GetNavigationMappingName(fkey, dependentKeyColumn);
                        if (dependentNavigationName == null)
                        {
                            if (dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName)
                                dependentNavigationName = "Parent";
                            else
                                dependentNavigationName = _pluralizer.Singularize(principalKeyColumn.TableName);

                            (String, String, String) dependentKey = (fkey.ConstraintSchema, dependentKeyColumn.TableName, principalKeyColumn.TableName);
                            if (navigationCounter.TryGetValue(dependentKey, out int counter))
                                counter++;
                            else
                                counter = 1;
                            navigationCounter[dependentKey] = counter;

                            IReadOnlyList<Column> dependentColumns = GetColumns(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName);
                            counter = GetCount(dependentColumns, dependentNavigationName, counter);

                            if (counter > 1)
                                dependentNavigationName += counter.ToString(CultureInfo.InvariantCulture);
                        }

                        String principalNavigationName = GetNavigationMappingName(fkey, principalKeyColumn);
                        if (principalNavigationName == null)
                        {
                            if (dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName)
                                principalNavigationName = "Children";
                            else
                                principalNavigationName = _pluralizer.Pluralize(dependentKeyColumn.TableName);

                            (String, String, String) principalKey = (fkey.ConstraintSchema, principalKeyColumn.TableName, dependentKeyColumn.TableName);
                            if (navigationCounter.TryGetValue(principalKey, out int counter))
                                counter++;
                            else
                                counter = 1;
                            navigationCounter[principalKey] = counter;

                            IReadOnlyList<Column> principalColumns = GetColumns(principalKeyColumn.TableSchema, principalKeyColumn.TableName);
                            counter = GetCount(principalColumns, principalNavigationName, counter);

                            if (counter > 1)
                                principalNavigationName += counter.ToString(CultureInfo.InvariantCulture);
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
        public IReadOnlyDictionary<(String tableSchema, String tableName), String> GetPrimaryKeyConstraintNames()
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
        public IReadOnlyList<OeOperationConfiguration> GetRoutines(DynamicTypeDefinitionManager typeDefinitionManager, InformationSchemaMapping informationSchemaMapping)
        {
            if (_routines == null)
            {
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    var routineParameters = new Dictionary<(String specificSchema, String specificName), IReadOnlyList<Parameter>>();
                    foreach (Parameter parameter in schemaContext.Parameters.Where(p => p.ParameterName != ""))
                    {
                        List<Parameter> parameterList;
                        if (routineParameters.TryGetValue((parameter.SpecificSchema, parameter.SpecificName), out IReadOnlyList<Parameter> parameters))
                            parameterList = (List<Parameter>)parameters;
                        else
                        {
                            parameterList = new List<Parameter>();
                            routineParameters.Add((parameter.SpecificSchema, parameter.SpecificName), parameterList);
                        }
                        parameterList.Add(parameter);
                    }

                    var operationMappings = new Dictionary<(String schema, String name), OperationMapping>(informationSchemaMapping.Operations.Count);
                    for (int i = 0; i < informationSchemaMapping.Operations.Count; i++)
                    {
                        OperationMapping operationMapping = informationSchemaMapping.Operations[i];
                        String[] nameParsed = operationMapping.DbName.Split('.');
                        operationMappings.Add((nameParsed[0], nameParsed[1]), operationMapping);
                    }

                    _routines = new List<OeOperationConfiguration>();
                    foreach (Routine routine in schemaContext.Routines)
                    {
                        if (operationMappings.TryGetValue((routine.RoutineSchema, routine.RoutineName), out OperationMapping operationMapping) && operationMapping.Exclude)
                            continue;

                        OeOperationParameterConfiguration[] parameterConfigurations = Array.Empty<OeOperationParameterConfiguration>();
                        if (routineParameters.TryGetValue((routine.SpecificSchema, routine.SpecificName), out IReadOnlyList<Parameter> parameters))
                        {
                            parameterConfigurations = new OeOperationParameterConfiguration[parameters.Count];
                            for (int i = 0; i < parameters.Count; i++)
                            {
                                Type clrType = _informationSchema.GetColumnClrType(parameters[i].DataType);
                                if (clrType.IsValueType)
                                    clrType = typeof(Nullable<>).MakeGenericType(clrType);

                                String parameterName = _informationSchema.GetParameterName(parameters[i].ParameterName);
                                parameterConfigurations[parameters[i].OrdinalPosition - 1] = new OeOperationParameterConfiguration(parameterName, clrType);
                            }
                        }

                        Type returnType = null;
                        if (routine.DataType != null)
                            returnType = _informationSchema.GetColumnClrType(routine.DataType);

                        if (returnType == null && operationMapping != null && operationMapping.ResultTableDbName != null)
                        {
                            String[] nameParsed = operationMapping.ResultTableDbName.Split('.');
                            String edmName = GetTableEdmName(nameParsed[0], nameParsed[1]);
                            returnType = typeDefinitionManager.GetDynamicTypeDefinition(edmName).DynamicTypeType;
                            returnType = typeof(IEnumerable<>).MakeGenericType(returnType);
                        }

                        _routines.Add(new OeOperationConfiguration(routine.RoutineSchema, routine.RoutineName,
                            typeof(Types.DynamicDbContext).Namespace, parameterConfigurations, returnType, routine.DataType != null));
                    }
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }
            }

            return _routines;
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
            return _tableEdmNameFullNames;
        }
        public void Initialize(IReadOnlyCollection<TableMapping> tableMappings)
        {
            _tableEdmNameFullNames = new Dictionary<String, (String tableSchema, String tableName, bool isQueryType)>();
            _tableFullNameEdmNames = new Dictionary<(String tableSchema, String tableName), String>();
            _navigationMappings = new Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>>();

            SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
            try
            {
                Dictionary<String, TableMapping> dbNameTableMappings = null;
                if (tableMappings != null)
                    dbNameTableMappings = tableMappings.ToDictionary(t => t.DbName, StringComparer.InvariantCultureIgnoreCase);

                var fixTableNames = new List<String>();
                List<Table> tables = schemaContext.Tables.ToList();
                foreach (Table table in tables)
                {
                    String tableName = table.TableName;
                    if (_tableEdmNameFullNames.ContainsKey(tableName))
                    {
                        fixTableNames.Add(tableName);
                        tableName = table.TableSchema + table.TableName;
                    }

                    if (dbNameTableMappings != null)
                    {
                        if (dbNameTableMappings.TryGetValue(table.TableName, out TableMapping tableMapping) ||
                        dbNameTableMappings.TryGetValue(table.TableSchema + "." + table.TableName, out tableMapping))
                        {
                            if (tableMapping.Exclude)
                                continue;

                            if (!String.IsNullOrEmpty(tableMapping.EdmName))
                            {
                                tableName = tableMapping.EdmName;
                                if (_tableEdmNameFullNames.ContainsKey(tableName))
                                    throw new InvalidOperationException("Duplicate TableMapping.EdmName = '" + tableName + "'");
                            }

                            if (tableMapping.Navigations != null && tableMapping.Navigations.Count > 0)
                                _navigationMappings.Add((table.TableSchema, table.TableName), tableMapping.Navigations);
                        }
                        else
                            continue;
                    }

                    _tableEdmNameFullNames.Add(tableName, (table.TableSchema, table.TableName, table.TableType == "VIEW"));
                    _tableFullNameEdmNames.Add((table.TableSchema, table.TableName), tableName);
                }

                foreach (String tableName in fixTableNames)
                {
                    int index = tables.FindIndex(t => t.TableName == tableName);
                    _tableEdmNameFullNames[tables[index].TableSchema + tables[index].TableName] = (tables[index].TableSchema, tables[index].TableName, tables[index].TableType == "VIEW");
                }
            }
            finally
            {
                _informationSchema.SchemaContextPool.Return(schemaContext);
            }
        }
    }
}
