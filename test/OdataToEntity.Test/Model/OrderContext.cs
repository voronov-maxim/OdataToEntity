using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderContext : DbContext
    {
        private sealed class ZStateManager : StateManager
        {
            public ZStateManager(IInternalEntityEntryFactory factory, IInternalEntityEntrySubscriber subscriber, IInternalEntityEntryNotifier notifier, IValueGenerationManager valueGeneration, IModel model, IDatabase database, IConcurrencyDetector concurrencyDetector, ICurrentDbContext currentContext)
                : base(factory, subscriber, notifier, valueGeneration, model, database, concurrencyDetector, currentContext)
            {
            }
            protected override async Task<int> SaveChangesAsync(IReadOnlyList<InternalEntityEntry> entriesToSave, CancellationToken cancellationToken = default(CancellationToken))
            {
                int count = await base.SaveChangesAsync(entriesToSave, cancellationToken);
                UpdateTemporaryKey(entriesToSave);
                return count;
            }
            internal static void UpdateTemporaryKey(IReadOnlyList<IUpdateEntry> entries)
            {
                foreach (IUpdateEntry entry in entries)
                    foreach (IKey key in entry.EntityType.GetKeys())
                        foreach (IProperty property in key.Properties)
                            if (entry.HasTemporaryValue(property))
                            {
                                Object value = entry.GetCurrentValue(property);
                                entry.SetCurrentValue(property, value);
                            }
            }

        }

        private OrderContext(DbContextOptions options) : base(options)
        {
        }

        public static OrderContext Create(String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName);
            optionsBuilder.ReplaceService<IStateManager, ZStateManager>();

            //optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;User ID=sa;Password=123456");

            return new OrderContext(optionsBuilder.Options);
        }
        public static String GenerateDatabaseName()
        {
            return Guid.NewGuid().ToString();
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
