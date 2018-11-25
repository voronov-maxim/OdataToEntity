using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
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
        //private static readonly LoggerFactory MyLoggerFactory = new LoggerFactory(new[] {new ConsoleLoggerProvider((category, level)
        //    => true, true) });

        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseSqlite(GetConnection(databaseName));
            optionsBuilder.ReplaceService<IEntityQueryModelVisitorFactory, ZQueryModelVisitorFactory>();
            //optionsBuilder.UseLoggerFactory(MyLoggerFactory);
            return optionsBuilder.Options;
        }
        public static DbContextOptions CreateClientEvaluationWarning(bool useRelationalNulls, String databaseName)
        {
            return Create(useRelationalNulls, databaseName);
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

    public sealed class OrderDbDataAdapter : EfCore.OeEfCoreDataAdapter<OrderContext>
    {
        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) :
            base(OrderContextOptions.Create(useRelationalNulls, databaseName), new Cache.OeQueryCache(allowCache))
        {
        }
    }
}
