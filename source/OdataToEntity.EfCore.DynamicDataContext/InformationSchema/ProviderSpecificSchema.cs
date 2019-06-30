using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public abstract class ProviderSpecificSchema : IDisposable
    {
        protected ProviderSpecificSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions, DbContextPool<SchemaContext> schemaContextPool)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            SchemaContextPool = schemaContextPool;
        }

        public virtual DynamicMetadataProvider CreateMetadataProvider(InformationSchemaMapping informationSchemaMapping)
        {
            return new DynamicMetadataProvider(this, informationSchemaMapping);
        }
        public void Dispose()
        {
            SchemaContextPool.Dispose();
        }
        public abstract Type GetColumnClrType(String dataType);
        public abstract IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns();
        public abstract String GetParameterName(String parameterName);

        public abstract bool IsCaseSensitive { get; }
        public DbContextOptions<Types.DynamicDbContext> DynamicDbContextOptions { get; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public abstract OeEfCoreOperationAdapter OperationAdapter { get; }
        public DbContextPool<SchemaContext> SchemaContextPool { get; }
    }
}
