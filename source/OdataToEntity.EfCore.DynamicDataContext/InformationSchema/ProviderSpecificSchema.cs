using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public abstract class ProviderSpecificSchema : IDisposable
    {
        protected ProviderSpecificSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions, DbContextPool<SchemaContext> schemaContextPool)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            SchemaContextPool = schemaContextPool;
        }

        public void Dispose()
        {
            SchemaContextPool.Dispose();
        }
        public abstract Type GetColumnClrType(String dataType);
        public abstract IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns();

        public DbContextOptions<Types.DynamicDbContext> DynamicDbContextOptions { get; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public abstract DynamicOperationAdapter OperationAdapter { get; }
        public IReadOnlyList<OeOperationConfiguration> GetRoutines()
        {
            SchemaContext schemaContext = SchemaContextPool.Rent();
            try
            {
                var routineParameters = new Dictionary<(String specificSchema, String specificName), List<Parameter>>();
                foreach (Parameter parameter in schemaContext.Parameters.Where(p => p.ParameterName != ""))
                {
                    if (!routineParameters.TryGetValue((parameter.SpecificSchema, parameter.SpecificName), out List<Parameter> parameters))
                    {
                        parameters = new List<Parameter>();
                        routineParameters.Add((parameter.SpecificSchema, parameter.SpecificName), parameters);
                    }
                    parameters.Add(parameter);
                }

                var routines = new List<OeOperationConfiguration>();
                foreach (Routine routine in schemaContext.Routines)
                {
                    OeOperationParameterConfiguration[] parameterConfigurations = Array.Empty<OeOperationParameterConfiguration>();
                    if (routineParameters.TryGetValue((routine.SpecificSchema, routine.SpecificName), out List<Parameter> parameters))
                    {
                        parameterConfigurations = new OeOperationParameterConfiguration[parameters.Count];
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            Type clrType = GetColumnClrType(parameters[i].DataType);
                            parameterConfigurations[parameters[i].OrdinalPosition - 1] = new OeOperationParameterConfiguration(parameters[i].ParameterName, clrType);
                        }
                    }

                    Type returnType = null;
                    if (routine.DataType != null)
                        returnType = GetColumnClrType(routine.DataType);

                    routines.Add(new OeOperationConfiguration(routine.RoutineSchema, routine.RoutineName,
                        typeof(Types.DynamicDbContext).Namespace, parameterConfigurations, returnType, routine.DataType != null));
                }
                return routines;
            }
            finally
            {
                SchemaContextPool.Return(schemaContext);
            }
        }
        public DbContextPool<SchemaContext> SchemaContextPool { get; }
    }
}
