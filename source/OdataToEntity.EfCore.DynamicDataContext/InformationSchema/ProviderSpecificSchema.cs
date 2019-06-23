using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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

            OperationAdapter = new OeEfCoreOperationAdapter(typeof(Types.DynamicDbContext));
        }

        public void Dispose()
        {
            SchemaContextPool.Dispose();
        }
        public abstract Type GetColumnClrType(String dataType);
        public abstract IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns();
        public abstract String GetParameterName(String parameterName);

        public DbContextOptions<Types.DynamicDbContext> DynamicDbContextOptions { get; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public virtual OeEfCoreOperationAdapter OperationAdapter { get; }
        public DbContextPool<SchemaContext> SchemaContextPool { get; }
    }
}
