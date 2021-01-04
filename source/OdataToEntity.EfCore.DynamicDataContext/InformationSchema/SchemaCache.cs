using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SchemaCache : IDisposable
    {
        private readonly Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> _keyColumns;
        private readonly Dictionary<TableFullName, List<(String constraintName, bool isPrimary)>> _keyConstraintNames;
        private readonly Dictionary<TableFullName, List<(String NavigationName, TableFullName ManyToManyTarget)>> _manyToManyProperties;
        private List<OeOperationConfiguration>? _routines;
        private readonly Dictionary<TableFullName, List<Column>> _tableColumns;
        private readonly Dictionary<TableFullName, List<Navigation>> _tableNavigations;
        private readonly Dictionary<TableFullName, (String tableEdmName, bool isQueryType)> _tableFullNameEdmNames;

        private readonly ProviderSpecificSchema _informationSchema;

        internal SchemaCache(
            ProviderSpecificSchema informationSchema,
            Dictionary<(String constraintSchema, String constraintName), IReadOnlyList<KeyColumnUsage>> keyColumns,
            Dictionary<TableFullName, (String tableEdmName, bool isQueryType)> tableFullNameEdmNames,
            Dictionary<TableFullName, List<Column>> tableColumns,
            Dictionary<TableFullName, List<(String constraintName, bool isPrimary)>> keyConstraintNames,
            Dictionary<TableFullName, List<Navigation>> tableNavigations,
            Dictionary<TableFullName, List<(String NavigationName, TableFullName ManyToManyTarget)>> manyToManyProperties)
        {
            _informationSchema = informationSchema;
            _keyColumns = keyColumns;
            _tableFullNameEdmNames = tableFullNameEdmNames;
            _tableColumns = tableColumns;
            _keyConstraintNames = keyConstraintNames;
            _tableNavigations = tableNavigations;
            _manyToManyProperties = manyToManyProperties;
        }

        public void Dispose()
        {
            _informationSchema.Dispose();
        }
        public IReadOnlyList<Column> GetColumns(in TableFullName tableFullName)
        {
            return _tableColumns[tableFullName];
        }
        public IReadOnlyList<KeyColumnUsage> GetKeyColumns(String constraintSchema, String constraintName)
        {
            return _keyColumns[(constraintSchema, constraintName)];
        }
        public IReadOnlyList<(String constraintName, bool isPrimary)> GetKeyConstraintNames(in TableFullName tableFullName)
        {
            if (_keyConstraintNames.TryGetValue(tableFullName, out List<(String constraintName, bool isPrimary)>? constraints))
                return constraints;

            return Array.Empty<(String constraintName, bool isPrimary)>();
        }
        public IReadOnlyList<(String NavigationName, TableFullName ManyToManyTarget)> GetManyToManyProperties(in TableFullName tableFullName)
        {
            if (_manyToManyProperties.TryGetValue(tableFullName, out List<(String NavigationName, TableFullName ManyToManyTarget)>? tableManyToManyProperties))
                return tableManyToManyProperties;

            return Array.Empty<(String NavigationName, TableFullName ManyToManyTarget)>();
        }
        public IReadOnlyList<Navigation> GetNavigations(in TableFullName tableFullName)
        {
            if (_tableNavigations.TryGetValue(tableFullName, out List<Navigation>? navigations))
                return navigations;

            return Array.Empty<Navigation>();
        }
        public IReadOnlyList<OeOperationConfiguration> GetRoutines(DynamicTypeDefinitionManager typeDefinitionManager, InformationSchemaSettings informationSchemaSettings)
        {
            if (_routines == null)
            {
                using SchemaContext schemaContext = _informationSchema.GetSchemaContext();

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

                        var tableFullName = new TableFullName(operationMapping.ResultTableDbName.Substring(0, i), operationMapping.ResultTableDbName.Substring(i + 1));
                        returnType = typeDefinitionManager.GetDynamicTypeDefinition(tableFullName).DynamicTypeType;
                        returnType = typeof(IEnumerable<>).MakeGenericType(returnType);
                    }

                    _routines.Add(new OeOperationConfiguration(routine.RoutineSchema, routine.RoutineName,
                        typeof(DynamicDbContext).Namespace!, parameterConfigurations, returnType ?? typeof(void), routine.DataType != null));
                }
            }

            return _routines;
        }
        public String GetTableEdmName(in TableFullName tableFullName)
        {
            return _tableFullNameEdmNames[tableFullName].tableEdmName;
        }
        public ICollection<TableFullName> GetTableFullNames()
        {
            return _tableFullNameEdmNames.Keys;
        }
        public bool IsQueryType(in TableFullName tableFullName)
        {
            return _tableFullNameEdmNames[tableFullName].isQueryType;
        }
    }
}
