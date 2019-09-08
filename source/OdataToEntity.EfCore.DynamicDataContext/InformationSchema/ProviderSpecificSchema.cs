using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public abstract class ProviderSpecificSchema : IDisposable
    {
        protected ProviderSpecificSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions, DbContextPool<SchemaContext> schemaContextPool)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            SchemaContextPool = schemaContextPool;
        }

        public DynamicMetadataProvider CreateMetadataProvider()
        {
            return CreateMetadataProvider(null);
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

        public bool IsCaseSensitive { get; protected set; }
        public DbContextOptions<DynamicDbContext> DynamicDbContextOptions { get; }
        public ExpressionVisitor ExpressionVisitor { get; protected set; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public OeEfCoreOperationAdapter OperationAdapter { get; protected set; }
        public DbContextPool<SchemaContext> SchemaContextPool { get; }
    }
}
