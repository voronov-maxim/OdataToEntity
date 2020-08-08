using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.ModelBuilder;
using System;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public readonly struct DynamicSchemaFactory
    {
        private readonly String _connectionString;
        private readonly String _provider;

        public DynamicSchemaFactory(String provider, String connectionString)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            _provider = provider.ToLowerInvariant();
            _connectionString = connectionString;
        }

        public ProviderSpecificSchema CreateSchema(bool useRelationalNulls)
        {
            return _provider switch
            {
                "mysql" => new MySqlSchema(CreateOptionsMySql(useRelationalNulls)),
                "postgresql" => new PostgreSqlSchema(CreateOptionsPostgreSql(useRelationalNulls)),
                "sqlserver" => new SqlServerSchema(CreateOptionsSqlServer(useRelationalNulls)),
                _ => throw new InvalidOperationException("Unkown provider " + _provider),
            };
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsSqlServer(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            //optionsBuilder.ReplaceService<IConventionSetBuilder, DynamicConventionSetBuilder>();
            return optionsBuilder.Options;
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsPostgreSql(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.UseNpgsql(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            //optionsBuilder.ReplaceService<IConventionSetBuilder, DynamicConventionSetBuilder>();
            return optionsBuilder.Options;
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsMySql(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.UseMySql(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            //optionsBuilder.ReplaceService<IConventionSetBuilder, DynamicConventionSetBuilder>();
            return optionsBuilder.Options;
        }
    }
}
