using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        private sealed class PatcherVisitor : ExpressionVisitor
        {
            protected override Expression VisitExtension(Expression node)
            {
                return node;
            }
            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression.NodeType == ExpressionType.Convert &&
                    (node.Member.DeclaringType == typeof(DateTime) || node.Member.DeclaringType == typeof(DateTimeOffset)))
                {
                    var propertyExpression = (MemberExpression)((UnaryExpression)node.Expression).Operand;
                    MethodInfo getValueOrDefault = propertyExpression.Type.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                    MethodCallExpression getValueOrDefaultCall = Expression.Call(propertyExpression, getValueOrDefault);
                    return Expression.MakeMemberAccess(getValueOrDefaultCall, node.Member);
                }

                return base.VisitMember(node);
            }
        }

        private sealed class ZQueryModelVisitor : RelationalQueryModelVisitor
        {
            public ZQueryModelVisitor(EntityQueryModelVisitorDependencies dependencies, RelationalQueryModelVisitorDependencies relationalDependencies, RelationalQueryCompilationContext queryCompilationContext, RelationalQueryModelVisitor parentQueryModelVisitor)
                : base(dependencies, relationalDependencies, queryCompilationContext, parentQueryModelVisitor)
            {
            }

            public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
            {
                var substringVisitor = new PatcherVisitor();
                whereClause.Predicate = substringVisitor.Visit(whereClause.Predicate);
                base.VisitWhereClause(whereClause, queryModel, index);
            }
        }

        private sealed class ZQueryModelVisitorFactory : RelationalQueryModelVisitorFactory
        {
            public ZQueryModelVisitorFactory(EntityQueryModelVisitorDependencies dependencies, RelationalQueryModelVisitorDependencies relationalDependencies) :
                base(dependencies, relationalDependencies)
            {
            }

            public override EntityQueryModelVisitor Create(QueryCompilationContext queryCompilationContext, EntityQueryModelVisitor parentEntityQueryModelVisitor)
            {
                return new ZQueryModelVisitor(base.Dependencies, base.RelationalDependencies,
                    (RelationalQueryCompilationContext)queryCompilationContext, (RelationalQueryModelVisitor)parentEntityQueryModelVisitor);
            }
        }

        private static readonly ConcurrentDictionary<String, SqliteConnection> _connections = new ConcurrentDictionary<String, SqliteConnection>();

        public static IEdmModel BuildDbEdmModel(bool useRelationalNulls, bool isDatabaseNullHighestValue)
        {
            return DbFixtureInitDb.CreateEdmModel();
        }
        public static DbContextOptions Create(String databaseName)
        {
            return Create<OrderContext>(databaseName);
        }
        public static DbContextOptions Create<T>(String databaseName) where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseSqlite(GetConnection(databaseName));
            optionsBuilder.ReplaceService<IEntityQueryModelVisitorFactory, ZQueryModelVisitorFactory>();
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
