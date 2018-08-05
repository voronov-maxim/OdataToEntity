using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        private sealed class ZInMemoryQueryModelVisitor : InMemoryQueryModelVisitor
        {
            public ZInMemoryQueryModelVisitor(EntityQueryModelVisitorDependencies dependencies, QueryCompilationContext queryCompilationContext)
                : base(dependencies, queryCompilationContext)
            {
            }

            public override void VisitOrdering(Ordering ordering, QueryModel queryModel, OrderByClause orderByClause, int index)
            {
                if (ordering.Expression.NodeType == ExpressionType.Convert)
                {
                    var convertExpression = ordering.Expression as UnaryExpression;
                    if (convertExpression.Type != convertExpression.Operand.Type)
                        ordering.Expression = Expression.Convert(convertExpression.Operand, convertExpression.Operand.Type);
                }
                base.VisitOrdering(ordering, queryModel, orderByClause, index);
            }
        }

        private sealed class ZInMemoryQueryModelVisitorFactory : InMemoryQueryModelVisitorFactory
        {
            public ZInMemoryQueryModelVisitorFactory(EntityQueryModelVisitorDependencies dependencies) : base(dependencies)
            {
            }

            public override EntityQueryModelVisitor Create(QueryCompilationContext queryCompilationContext, EntityQueryModelVisitor parentEntityQueryModelVisitor)
            {
                return new ZInMemoryQueryModelVisitor(base.Dependencies, queryCompilationContext);
            }
        }

        private sealed class ZStateManager : StateManager
        {
            public ZStateManager(StateManagerDependencies dependencies) : base(dependencies)
            {

            }
            protected override async Task<int> SaveChangesAsync(IReadOnlyList<InternalEntityEntry> entriesToSave, CancellationToken cancellationToken = default)
            {
                UpdateTemporaryKey(entriesToSave);
                int count = await base.SaveChangesAsync(entriesToSave, cancellationToken).ConfigureAwait(false);
                return count;
            }
            internal static void UpdateTemporaryKey(IReadOnlyList<InternalEntityEntry> entries)
            {
                foreach (InternalEntityEntry entry in entries)
                    foreach (IKey key in entry.EntityType.GetKeys())
                        foreach (IProperty property in key.Properties)
                            if (entry.HasTemporaryValue(property))
                            {
                                int id = (int)entry.GetCurrentValue(property);
                                entry.SetProperty(property, -id, false);
                            }
            }

        }

        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName);
            optionsBuilder.ReplaceService<IStateManager, ZStateManager>();
            optionsBuilder.ReplaceService<IEntityQueryModelVisitorFactory, ZInMemoryQueryModelVisitorFactory>();
            return optionsBuilder.Options;
        }
    }
}
