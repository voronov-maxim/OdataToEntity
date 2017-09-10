using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.Model
{
    public sealed partial class OrderContext : DbContext
    {
        private sealed class ZStateManager : StateManager
        {
            public ZStateManager(IInternalEntityEntryFactory factory, IInternalEntityEntrySubscriber subscriber, IInternalEntityEntryNotifier notifier, IValueGenerationManager valueGeneration, IModel model, IDatabase database, IConcurrencyDetector concurrencyDetector, ICurrentDbContext currentContext, ILoggingOptions loggingOptions, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
                : base(factory, subscriber, notifier, valueGeneration, model, database, concurrencyDetector, currentContext, loggingOptions, updateLogger)
            {
            }
            protected override async Task<int> SaveChangesAsync(IReadOnlyList<InternalEntityEntry> entriesToSave, CancellationToken cancellationToken = default(CancellationToken))
            {
                UpdateTemporaryKey(entriesToSave);
                int count = await base.SaveChangesAsync(entriesToSave, cancellationToken);
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

        public static OrderContext Create(String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName);
            optionsBuilder.ReplaceService<IStateManager, ZStateManager>();

            return new OrderContext(optionsBuilder.Options);
        }
        public static String GenerateDatabaseName()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
