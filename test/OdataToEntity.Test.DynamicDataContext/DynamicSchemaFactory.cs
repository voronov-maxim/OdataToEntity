using Microsoft.EntityFrameworkCore;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
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
            switch (_provider)
            {
                case "mysql":
                    return new MySqlSchema(CreateOptionsMySql(useRelationalNulls));
                case "postgresql":
                    return new PostgreSqlSchema(CreateOptionsPostgreSql(useRelationalNulls));
                case "sqlserver":
                    return new SqlServerSchema(CreateOptionsSqlServer(useRelationalNulls));
                default:
                    throw new InvalidOperationException("Unkown provider " + _provider);
            }
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsSqlServer(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsPostgreSql(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.UseNpgsql(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        private DbContextOptions<DynamicDbContext> CreateOptionsMySql(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DynamicDbContext>();
            optionsBuilder.UseMySql(_connectionString, opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}
