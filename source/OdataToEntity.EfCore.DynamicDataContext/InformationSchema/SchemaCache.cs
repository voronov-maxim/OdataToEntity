using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SchemaCache : IDisposable
    {
        private readonly Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> _keyColumns;
        private readonly Dictionary<(String tableSchema, String tableName), List<(String constraintName, bool isPrimary)>> _keyConstraintNames;
        private readonly Dictionary<String, List<(String NavigationName, String ManyToManyTarget)>> _manyToManyProperties;
        private List<OeOperationConfiguration>? _routines;
        private readonly Dictionary<(String tableSchema, String tableName), List<Column>> _tableColumns;
        private readonly Dictionary<(String tableSchema, String tableName), List<Navigation>> _tableNavigations;
        private readonly Dictionary<String, (String tableSchema, String tableName, bool isQueryType)> _tableEdmNameFullNames;
        private readonly Dictionary<(String tableSchema, String tableName), String> _tableFullNameEdmNames;

        private readonly ProviderSpecificSchema _informationSchema;

        internal SchemaCache(
            ProviderSpecificSchema informationSchema,
            Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns,
            Dictionary<String, (String tableSchema, String tableName, bool isQueryType)> tableEdmNameFullNames,
            Dictionary<(String tableSchema, String tableName), String> tableFullNameEdmNames,
            Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> navigationMappings,
            Dictionary<(String tableSchema, String tableName), List<Column>> tableColumns,
            Dictionary<(String tableSchema, String tableName), List<(String constraintName, bool isPrimary)>> keyConstraintNames,
            Dictionary<(String tableSchema, String tableName), List<Navigation>> tableNavigations)
        {
            _informationSchema = informationSchema;
            _keyColumns = keyColumns;
            _tableEdmNameFullNames = tableEdmNameFullNames;
            _tableFullNameEdmNames = tableFullNameEdmNames;
            _tableColumns = tableColumns;
            _keyConstraintNames = keyConstraintNames;
            _tableNavigations = tableNavigations;

            _manyToManyProperties = GetManyToManyProperties(tableFullNameEdmNames, navigationMappings);
        }

        public void Dispose()
        {
            _informationSchema.Dispose();
        }
        public IReadOnlyList<Column> GetColumns(String tableEdmName)
        {
            return _tableColumns[GetTableFullName(tableEdmName)];
        }
        public IReadOnlyList<KeyColumnUsage> GetKeyColumns(String constraintSchema, String constraintName)
        {
            return _keyColumns[(constraintSchema, constraintName)];
        }
        public IReadOnlyList<(String constraintName, bool isPrimary)> GetKeyConstraintNames(String tableEdmName)
        {
            if (_keyConstraintNames.TryGetValue(GetTableFullName(tableEdmName), out List<(String constraintName, bool isPrimary)>? constraints))
                return constraints;

            return Array.Empty<(String constraintName, bool isPrimary)>();
        }
        public IReadOnlyList<(String NavigationName, String ManyToManyTarget)> GetManyToManyProperties(String tableEdmName)
        {
            if (_manyToManyProperties.TryGetValue(tableEdmName, out List<(String NavigationName, String ManyToManyTarget)>? tableManyToManyProperties))
                return tableManyToManyProperties;

            return Array.Empty<(String NavigationName, String ManyToManyTarget)>();
        }
        private static Dictionary<String, List<(String NavigationName, String ManyToManyTarget)>> GetManyToManyProperties(
            Dictionary<(String tableSchema, String tableName), String> tableFullNameEdmNames,
            Dictionary<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> navigationMappings)
        {
            var manyToManyProperties = new Dictionary<String, List<(String NavigationName, String ManyToManyTarget)>>();
            foreach (KeyValuePair<(String tableSchema, String tableName), IReadOnlyList<NavigationMapping>> pair in navigationMappings)
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    NavigationMapping navigationMapping = pair.Value[i];
                    if (!String.IsNullOrEmpty(navigationMapping.ManyToManyTarget))
                    {
                        String tableEdmName = tableFullNameEdmNames[(pair.Key.tableSchema, pair.Key.tableName)];
                        if (!manyToManyProperties.TryGetValue(tableEdmName, out List<(String NavigationName, String ManyToManyTarget)>? manyToManies))
                        {
                            manyToManies = new List<(String NavigationName, String ManyToManyTarget)>();
                            manyToManyProperties.Add(tableEdmName, manyToManies);
                        }

                        if (navigationMapping.NavigationName == null)
                            throw new InvalidOperationException("For ManyToManyTarget" + navigationMapping.ManyToManyTarget + " NavigationName must be not null");

                        manyToManies.Add((navigationMapping.NavigationName, navigationMapping.ManyToManyTarget));
                    }
                }

            return manyToManyProperties;
        }
        public IReadOnlyList<Navigation> GetNavigations(String tableEdmName)
        {
            if (_tableNavigations.TryGetValue(GetTableFullName(tableEdmName), out List<Navigation>? navigations))
                return navigations;

            return Array.Empty<Navigation>();
        }
        public IReadOnlyList<OeOperationConfiguration> GetRoutines(DynamicTypeDefinitionManager typeDefinitionManager, InformationSchemaSettings informationSchemaSettings)
        {
            if (_routines == null)
            {
                SchemaContext schemaContext = _informationSchema.GetSchemaContext();

                IQueryable<Parameter> parametersQuery = schemaContext.Parameters;
                IQueryable<Routine> routinesQuery = schemaContext.Routines;
                if (informationSchemaSettings.IncludedSchemas != null && informationSchemaSettings.IncludedSchemas.Count > 0)
                {
                    parametersQuery = parametersQuery.Where(t => informationSchemaSettings.IncludedSchemas.Contains(t.SpecificSchema));
                    routinesQuery = routinesQuery.Where(t => informationSchemaSettings.IncludedSchemas.Contains(t.RoutineSchema));
                }
                if (informationSchemaSettings.ExcludedSchemas != null && informationSchemaSettings.ExcludedSchemas.Count > 0)
                {
                    parametersQuery = parametersQuery.Where(t => !informationSchemaSettings.ExcludedSchemas.Contains(t.SpecificSchema));
                    routinesQuery = routinesQuery.Where(t => !informationSchemaSettings.ExcludedSchemas.Contains(t.RoutineSchema));
                }

                try
                {
                    var unsupportedRoutines = new HashSet<(String specificSchema, String specificName)>();
                    var routineParameters = new Dictionary<(String specificSchema, String specificName), List<Parameter>>();
                    foreach (Parameter parameter in parametersQuery)
                        if (String.IsNullOrEmpty(parameter.ParameterName) || _informationSchema.GetColumnClrType(parameter.DataType) == null)
                            unsupportedRoutines.Add((parameter.SpecificSchema, parameter.SpecificName));
                        else
                        {
                            if (!routineParameters.TryGetValue((parameter.SpecificSchema, parameter.SpecificName), out List<Parameter>? parameters))
                            {
                                parameters = new List<Parameter>();
                                routineParameters.Add((parameter.SpecificSchema, parameter.SpecificName), parameters);
                            }
                            parameters.Add(parameter);
                        }

                    Dictionary<(String schema, String name), OperationMapping>? operationMappings = null;
                    if (informationSchemaSettings.Operations != null)
                    {
                        operationMappings = new Dictionary<(String schema, String name), OperationMapping>(informationSchemaSettings.Operations.Count);
                        for (int i = 0; i < informationSchemaSettings.Operations.Count; i++)
                        {
                            OperationMapping operationMapping = informationSchemaSettings.Operations[i];
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
                    foreach (Routine routine in routinesQuery)
                    {
                        OperationMapping? operationMapping = null;
                        if (unsupportedRoutines.Contains((routine.SpecificSchema, routine.SpecificName)) ||
                            (operationMappings != null &&
                            operationMappings.TryGetValue((routine.RoutineSchema, routine.RoutineName), out operationMapping)
                            && operationMapping.Exclude))
                        {
                            if ((operationMapping != null && operationMapping.Exclude) || informationSchemaSettings.ObjectFilter == DbObjectFilter.Mapping)
                                continue;
                        }

                        OeOperationParameterConfiguration[] parameterConfigurations = Array.Empty<OeOperationParameterConfiguration>();
                        if (routineParameters.TryGetValue((routine.SpecificSchema, routine.SpecificName), out List<Parameter>? parameters))
                        {
                            Type? clrType = null;
                            parameterConfigurations = new OeOperationParameterConfiguration[parameters.Count];
                            for (int i = 0; i < parameters.Count; i++)
                            {
                                clrType = _informationSchema.GetColumnClrType(parameters[i].DataType);
                                if (clrType == null)
                                    break;

                                if (clrType.IsValueType)
                                    clrType = typeof(Nullable<>).MakeGenericType(clrType);

                                String parameterName = _informationSchema.GetParameterName(parameters[i].ParameterName!);
                                parameterConfigurations[parameters[i].OrdinalPosition - 1] = new OeOperationParameterConfiguration(parameterName, clrType);
                            }

                            if (parameters.Count > 0 && clrType == null)
                                continue;
                        }

                        Type? returnType = null;
                        if (routine.DataType != null)
                            returnType = _informationSchema.GetColumnClrType(routine.DataType);

                        if (returnType == null && operationMapping != null && operationMapping.ResultTableDbName != null)
                        {
                            int i = operationMapping.ResultTableDbName.IndexOf('.');
                            if (i == -1)
                                throw new InvalidOperationException("ResultTableDbName " + operationMapping.ResultTableDbName + " must contains schema");

                            String? edmName = GetTableEdmName(operationMapping.ResultTableDbName.Substring(0, i), operationMapping.ResultTableDbName.Substring(i + 1));
                            if (edmName == null)
                                continue;

                            returnType = typeDefinitionManager.GetDynamicTypeDefinition(edmName).DynamicTypeType;
                            returnType = typeof(IEnumerable<>).MakeGenericType(returnType);
                        }

                        _routines.Add(new OeOperationConfiguration(routine.RoutineSchema, routine.RoutineName,
                            typeof(DynamicDbContext).Namespace!, parameterConfigurations, returnType ?? typeof(void), routine.DataType != null));
                    }
                }
                finally
                {
                    schemaContext.Dispose();
                }
            }

            return _routines;
        }
        public String? GetTableEdmName(String tableSchema, String tableName)
        {
            _tableFullNameEdmNames.TryGetValue((tableSchema, tableName), out String? tableEdmName);
            return tableEdmName;
        }
        public IEnumerable<(String tableEdmName, bool isQueryType)> GetTableEdmNames()
        {
            foreach (var pair in _tableEdmNameFullNames)
                yield return (pair.Key, pair.Value.isQueryType);
        }
        public (String tableSchema, String tableName) GetTableFullName(String tableEdmName)
        {
            (String tableSchema, String tableName, _) = _tableEdmNameFullNames[tableEdmName];
            return (tableSchema, tableName);
        }
    }
}
