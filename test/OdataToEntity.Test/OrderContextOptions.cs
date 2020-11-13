using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using System;
using System.Collections.Concurrent;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        private static readonly ConcurrentDictionary<String, SqliteConnection> _connections = new ConcurrentDictionary<String, SqliteConnection>();

        public static IEdmModel BuildDbEdmModel(IEdmModel _, bool __)
        {
            return DbFixtureInitDb.CreateEdmModel();
        }
        public static DbContextOptions<OrderContext> Create(String databaseName)
        {
            return Create<OrderContext>(databaseName);
        }
        public static DbContextOptions Create(bool _)
        {
            throw new NotSupportedException();
        }
        public static DbContextOptions<T> Create<T>(String databaseName) where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseSqlite(GetConnection(databaseName));
            //optionsBuilder.UseLoggerFactory(CreateLoggerFactory());
            return optionsBuilder.Options;
        }
        public static DbContextOptions CreateClientEvaluationWarning(String databaseName)
        {
            return Create(databaseName);
        }
        private static ILoggerFactory CreateLoggerFactory()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole()).Configure<LoggerFilterOptions>(o => o.MinLevel = LogLevel.Information);
            return serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
        }
        private static SqliteConnection GetConnection(String databaseName)
        {
            if (!_connections.TryGetValue(databaseName, out SqliteConnection connection))
            {
                connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                if (!_connections.TryAdd(databaseName, connection))
                {
                    connection.Dispose();
                    return GetConnection(databaseName);
                }
            }

            return connection;
        }
    }
}