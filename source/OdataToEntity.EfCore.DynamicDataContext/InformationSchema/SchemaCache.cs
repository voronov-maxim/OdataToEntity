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

        private List<ReferentialConstraint> _referentialConstraints;
        private Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> _keyColumns;
        private Dictionary<(String tableSchema, String tableName), IReadOnlyList<(String constraintName, bool isPrimary)>> _keys;
        private Dictionary<String, IReadOnlyList<(String NavigationName, String ManyToManyTarget)>> _manyToManyProperties;
        private Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> _navigationMappings;
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
        public IReadOnlyDictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> GetKeyColumns()
        {
            if (_keyColumns == null)
            {
                var keyColumns = new Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>>();
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    String constraintSchema = null;
                    String constraintName = null;
                    List<KeyColumnUsage> columns = null;
                    foreach (KeyColumnUsage keyColumn in schemaContext.KeyColumnUsage
                        .OrderBy(t => t.TableSchema).ThenBy(t => t.TableName).ThenBy(t => t.ConstraintName).ThenBy(t => t.OrdinalPosition))
                    {
                        if (constraintSchema != keyColumn.ConstraintSchema || constraintName != keyColumn.ConstraintName)
                        {
                            if (columns != null)
                                keyColumns.Add((constraintSchema, constraintName), columns);

                            constraintSchema = keyColumn.ConstraintSchema;
                            constraintName = keyColumn.ConstraintName;
                            columns = new List<KeyColumnUsage>();
                        }
                        columns.Add(keyColumn);
                    }

                    if (columns != null)
                        keyColumns.Add((constraintSchema, constraintName), columns);
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }

                _keyColumns = keyColumns;
            }
            return _keyColumns;
        }
        public String GetFKeyConstraintName(String tableSchema, String tableName1, String tableName2)
        {
            IReadOnlyDictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns = GetKeyColumns();
            foreach (ReferentialConstraint fkey in _referentialConstraints)
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
        public IReadOnlyDictionary<(String tableSchema, String tableName), IReadOnlyList<(String constraintName, bool isPrimary)>> GetKeyConstraintNames()
        {
            if (_keys == null)
            {
                SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
                try
                {
                    var tableConstraints = schemaContext.TableConstraints.Where(t => t.ConstraintType == "PRIMARY KEY" || t.ConstraintType == "UNIQUE")
                        .OrderBy(t => t.TableSchema).ThenBy(t => t.TableName).ThenBy(t => t.ConstraintType);

                    _keys = new Dictionary<(String tableSchema, String tableName), IReadOnlyList<(String constraintName, bool isPrimary)>>();
                    String tableSchema = null;
                    String tableName = null;
                    List<(String constraintName, bool isPrimary)> constraints = null;
                    foreach (TableConstraint tableConstraint in tableConstraints)
                    {
                        if (tableSchema != tableConstraint.TableSchema || tableName != tableConstraint.TableName)
                        {
                            if (constraints != null)
                                _keys.Add((tableSchema, tableName), constraints);

                            tableSchema = tableConstraint.TableSchema;
                            tableName = tableConstraint.TableName;
                            constraints = new List<(String constraintName, bool isPrimary)>();
                        }
                        constraints.Add((tableConstraint.ConstraintName, constraints.Count == 0));
                    }

                    if (constraints != null)
                        _keys.Add((tableSchema, tableName), constraints);
                }
                finally
                {
                    _informationSchema.SchemaContextPool.Return(schemaContext);
                }
            }
            return _keys;
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
                _tableNavigations = new Dictionary<(String tableSchema, String tableName), List<Navigation>>();
                IReadOnlyDictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns = GetKeyColumns();

                var navigationCounter = new Dictionary<(String, String, String), List<IReadOnlyList<KeyColumnUsage>>>();
                foreach (ReferentialConstraint fkey in _referentialConstraints)
                {
                    IReadOnlyList<KeyColumnUsage> dependentKeyColumns = keyColumns[(fkey.ConstraintSchema, fkey.ConstraintName)];

                    KeyColumnUsage dependentKeyColumn = dependentKeyColumns[0];
                    String principalEdmName = GetTableEdmName(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName);
                    if (principalEdmName == null)
                        continue;

                    KeyColumnUsage principalKeyColumn = keyColumns[(fkey.UniqueConstraintSchema, fkey.UniqueConstraintName)][0];
                    String dependentEdmName = GetTableEdmName(principalKeyColumn.TableSchema, principalKeyColumn.TableName);
                    if (dependentEdmName == null)
                        continue;

                    bool selfReferences = false;
                    String dependentNavigationName = GetNavigationMappingName(fkey, dependentKeyColumn);
                    if (dependentNavigationName == null)
                    {
                        selfReferences = dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName;
                        if (selfReferences)
                            dependentNavigationName = "Parent";
                        else
                            dependentNavigationName = _pluralizer.Singularize(dependentEdmName);

                        (String, String, String) dependentKey = (fkey.ConstraintSchema, dependentKeyColumn.TableName, principalKeyColumn.TableName);
                        if (navigationCounter.TryGetValue(dependentKey, out List<IReadOnlyList<KeyColumnUsage>> columnsList))
                        {
                            if (FKeyExist(columnsList, dependentKeyColumns))
                                continue;

                            columnsList.Add(dependentKeyColumns);
                        }
                        else
                        {
                            columnsList = new List<IReadOnlyList<KeyColumnUsage>>() { dependentKeyColumns };
                            navigationCounter[dependentKey] = columnsList;
                        }

                        IReadOnlyList<Column> dependentColumns = GetColumns(dependentKeyColumn.TableSchema, dependentKeyColumn.TableName);
                        dependentNavigationName = GetUniqueName(dependentColumns, dependentNavigationName, columnsList.Count);
                    }

                    String principalNavigationName = GetNavigationMappingName(fkey, principalKeyColumn);
                    if (principalNavigationName == null)
                    {
                        if (dependentKeyColumn.TableSchema == principalKeyColumn.TableSchema && dependentKeyColumn.TableName == principalKeyColumn.TableName)
                            principalNavigationName = "Children";
                        else
                            principalNavigationName = _pluralizer.Pluralize(principalEdmName);

                        (String, String, String) principalKey = (fkey.ConstraintSchema, principalKeyColumn.TableName, dependentKeyColumn.TableName);
                        if (navigationCounter.TryGetValue(principalKey, out List<IReadOnlyList<KeyColumnUsage>> columnsList))
                        {
                            if (!selfReferences)
                            {
                                if (FKeyExist(columnsList, dependentKeyColumns))
                                    continue;

                                columnsList.Add(dependentKeyColumns);
                            }
                        }
                        else
                        {
                            columnsList = new List<IReadOnlyList<KeyColumnUsage>>() { dependentKeyColumns };
                            navigationCounter[principalKey] = columnsList;
                        }

                        IReadOnlyList<Column> principalColumns = GetColumns(principalKeyColumn.TableSchema, principalKeyColumn.TableName);
                        principalNavigationName = GetUniqueName(principalColumns, principalNavigationName, columnsList.Count);
                    }

                    AddNavigation(fkey, dependentKeyColumn, dependentNavigationName, false);
                    AddNavigation(fkey, principalKeyColumn, principalNavigationName, true);
                }
            }
            return _tableNavigations;

            bool FKeyExist(List<IReadOnlyList<KeyColumnUsage>> keyColumnsList, IReadOnlyList<KeyColumnUsage> keyColumns)
            {
                for (int i = 0; i < keyColumnsList.Count; i++)
                    if (keyColumnsList[i].Count == keyColumns.Count)
                    {
                        int j = 0;
                        for (; j < keyColumns.Count; j++)
                            if (keyColumnsList[i][j].ColumnName != keyColumns[j].ColumnName)
                                break;

                        if (j == keyColumns.Count)
                            return true;
                    }

                return false;
            }
            int GetCountName(IReadOnlyList<Column> columns, String navigationName)
            {
                int counter = 0;
                for (int i = 0; i < columns.Count; i++)
                    if (String.Compare(navigationName, columns[i].ColumnName, StringComparison.OrdinalIgnoreCase) == 0)
                        counter++;
                return counter;
            }
            String GetUniqueName(IReadOnlyList<Column> columns, String navigationName, int counter)
            {
                int counter2;
                String navigationName2 = navigationName;
                do
                {
                    counter2 = GetCountName(columns, navigationName2);
                    counter += counter2;
                    navigationName2 = counter > 1 ? navigationName + counter.ToString(CultureInfo.InvariantCulture) : navigationName;
                }
                while (counter2 > 0 && GetCountName(columns, navigationName2) > 0);
                return navigationName2;
            }
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

                    Dictionary<(String schema, String name), OperationMapping> operationMappings = null;
                    if (informationSchemaMapping != null && informationSchemaMapping.Operations != null)
                    {
                        operationMappings = new Dictionary<(String schema, String name), OperationMapping>(informationSchemaMapping.Operations.Count);
                        for (int i = 0; i < informationSchemaMapping.Operations.Count; i++)
                        {
                            OperationMapping operationMapping = informationSchemaMapping.Operations[i];
                            int index = operationMapping.DbName.IndexOf('.');
                            if (index == -1)
                                operationMappings.Add(("", operationMapping.DbName), operationMapping);
                            else
                            {
                                String schema = operationMapping.DbName.Substring(0, index);
                                String name = operationMapping.DbName.Substring(index + 1);
                                operationMappings.Add((schema, name), operationMapping);
                            }
                        }
                    }

                    _routines = new List<OeOperationConfiguration>();
                    foreach (Routine routine in schemaContext.Routines)
                    {
                        OperationMapping operationMapping = null;
                        if (operationMappings != null &&
                            operationMappings.TryGetValue((routine.RoutineSchema, routine.RoutineName), out operationMapping) &&
                            operationMapping.Exclude)
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
                            int i = operationMapping.ResultTableDbName.IndexOf('.');
                            if (i == -1)
                                throw new InvalidOperationException("ResultTableDbName " + operationMapping.ResultTableDbName + " must contains schema");

                            String edmName = GetTableEdmName(operationMapping.ResultTableDbName.Substring(0, i), operationMapping.ResultTableDbName.Substring(i + 1));
                            returnType = typeDefinitionManager.GetDynamicTypeDefinition(edmName).DynamicTypeType;
                            returnType = typeof(IEnumerable<>).MakeGenericType(returnType);
                        }

                        _routines.Add(new OeOperationConfiguration(routine.RoutineSchema, routine.RoutineName,
                            typeof(DynamicDbContext).Namespace, parameterConfigurations, returnType, routine.DataType != null));
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
            IEqualityComparer<String> comparer = _informationSchema.IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            TupleStringComparer tupleComparer = _informationSchema.IsCaseSensitive ? TupleStringComparer.Ordinal : TupleStringComparer.OrdinalIgnoreCase;

            _tableEdmNameFullNames = new Dictionary<String, (String tableSchema, String tableName, bool isQueryType)>(comparer);
            _tableFullNameEdmNames = new Dictionary<(String tableSchema, String tableName), String>(tupleComparer);
            _navigationMappings = new Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>>(tupleComparer);

            SchemaContext schemaContext = _informationSchema.SchemaContextPool.Rent();
            try
            {
                _referentialConstraints = schemaContext.ReferentialConstraints.ToList();

                Dictionary<String, TableMapping> dbNameTableMappings = null;
                if (tableMappings != null)
                    dbNameTableMappings = tableMappings.ToDictionary(t => t.DbName, StringComparer.OrdinalIgnoreCase);

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
                            {
                                foreach (NavigationMapping navigationMapping in tableMapping.Navigations)
                                    if (!String.IsNullOrEmpty(navigationMapping.NavigationName) && String.IsNullOrEmpty(navigationMapping.ConstraintName))
                                    {
                                        String tableName2 = navigationMapping.TargetTableName;
                                        if (tableName2 != null)
                                        {
                                            int i = tableName2.IndexOf('.');
                                            if (i != -1)
                                                tableName2 = tableName2.Substring(i + 1);

                                            navigationMapping.ConstraintName = GetFKeyConstraintName(table.TableSchema, table.TableName, tableName2);
                                        }
                                    }

                                _navigationMappings.Add((table.TableSchema, table.TableName), tableMapping.Navigations);
                            }
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
