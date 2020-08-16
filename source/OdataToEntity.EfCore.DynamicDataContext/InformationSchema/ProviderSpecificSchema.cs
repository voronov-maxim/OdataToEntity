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
        private readonly DbContextPool<SchemaContext> _schemaContextPool;

        protected ProviderSpecificSchema(DbContextOptions<DynamicDbContext> dynamicDbContextOptions, DbContextPool<SchemaContext> schemaContextPool)
        {
            DynamicDbContextOptions = dynamicDbContextOptions;
            _schemaContextPool = schemaContextPool;
        }

        public DynamicMetadataProvider CreateMetadataProvider()
        {
            return CreateMetadataProvider(new InformationSchemaSettings());
        }
        public virtual DynamicMetadataProvider CreateMetadataProvider(InformationSchemaSettings informationSchemaSettings)
        {
            return new DynamicMetadataProvider(this, informationSchemaSettings);
        }
        public void Dispose()
        {
            _schemaContextPool.Dispose();
        }
        public abstract Type? GetColumnClrType(String dataType);
        public abstract IReadOnlyList<DbGeneratedColumn> GetDbGeneratedColumns();
        public abstract String GetParameterName(String parameterName);
        public SchemaContext GetSchemaContext()
        {
            return (SchemaContext)new DbContextLease(_schemaContextPool, true).Context;
        }

        public bool IsCaseSensitive { get; protected set; }
        public DbContextOptions<DynamicDbContext> DynamicDbContextOptions { get; }
        public ExpressionVisitor? ExpressionVisitor { get; protected set; }
        public bool IsDatabaseNullHighestValue { get; protected set; }
        public OeEfCoreOperationAdapter OperationAdapter { get; protected set; } = null!;
    }
}
