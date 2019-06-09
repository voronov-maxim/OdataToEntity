using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public abstract class ProviderSpecificSchema : IDisposable
    {
        protected ProviderSpecificSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            SchemaContextPool = new DbContextPool<SchemaContext>(CreateOptions(dynamicDbContextOptions));
        }
        protected ProviderSpecificSchema(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions, DbContextPool<SchemaContext> schemaContextPool)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            SchemaContextPool = schemaContextPool;
        }

        private static DbContextOptions<SchemaContext> CreateOptions(DbContextOptions<Types.DynamicDbContext> dynamicDbContextOptions)
        {
            DbContextOptions schemaOptions = new DbContextOptionsBuilder<SchemaContext>().Options;
            foreach (IDbContextOptionsExtension extension in dynamicDbContextOptions.Extensions)
                schemaOptions = schemaOptions.WithExtension(extension);
            return (DbContextOptions<SchemaContext>)schemaOptions;
        }
        public void Dispose()
        {
            SchemaContextPool.Dispose();
        }
        public abstract Type GetColumnClrType(String dataType);
        public abstract IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns();

        public DbContextOptions<Types.DynamicDbContext> DynamicDbContextOptions { get; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public DbContextPool<SchemaContext> SchemaContextPool { get; }
    }
}
