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
        public void InitDb()
        {
            var customer1 = new Customer()
            {
                Address = "Moscow",
                Id = 1,
                Name = "Ivan",
                Sex = Sex.Male
            };
            var customer2 = new Customer()
            {
                Address = "Tambov",
                Id = 2,
                Name = "Natasha",
                Sex = Sex.Female
            };
            var customer3 = new Customer()
            {
                Address = "Tula",
                Id = 3,
                Name = "Sasha",
                Sex = Sex.Female
            };
            var customer4 = new Customer()
            {
                Address = null,
                Id = 4,
                Name = "Unknown",
                Sex = null
            };

            var order1 = new Order()
            {
                Date = DateTimeOffset.Parse("2016-07-04T19:10:10.8237573+03:00"),
                Id = 1,
                Name = "Order 1",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order2 = new Order()
            {
                Date = DateTimeOffset.Parse("2016-07-04T19:10:11.0000000+03:00"),
                Id = 2,
                Name = "Order 2",
                CustomerId = 2,
                Status = OrderStatus.Processing
            };
            var order3 = new Order()
            {
                AltCustomerId = 3,
                Date = null,
                Id = 3,
                Name = "Order unknown",
                CustomerId = 4,
                Status = OrderStatus.Unknown
            };

            var orderItem11 = new OrderItem()
            {
                Count = 1,
                Id = 1,
                OrderId = 1,
                Price = 1.1m,
                Product = "Product order 1 item 1"
            };
            var orderItem12 = new OrderItem()
            {
                Count = 2,
                Id = 2,
                OrderId = 1,
                Price = 1.2m,
                Product = "Product order 1 item 2"
            };
            var orderItem13 = new OrderItem()
            {
                Count = 3,
                Id = 3,
                OrderId = 1,
                Price = 1.3m,
                Product = "Product order 1 item 3"
            };

            var orderItem21 = new OrderItem()
            {
                Count = 1,
                Id = 4,
                OrderId = 2,
                Price = 2.1m,
                Product = "Product order 2 item 1"
            };
            var orderItem22 = new OrderItem()
            {
                Count = 2,
                Id = 5,
                OrderId = 2,
                Price = 2.2m,
                Product = "Product order 2 item 2"
            };
            var orderItem31 = new OrderItem()
            {
                Count = null,
                Id = 6,
                OrderId = 3,
                Price = null,
                Product = "Product order 3 item 1 (unknown)"
            };

            Customers.Add(customer1);
            Customers.Add(customer2);
            Customers.Add(customer3);
            Customers.Add(customer4);
            Orders.Add(order1);
            Orders.Add(order2);
            Orders.Add(order3);
            OrderItems.Add(orderItem11);
            OrderItems.Add(orderItem12);
            OrderItems.Add(orderItem13);
            OrderItems.Add(orderItem21);
            OrderItems.Add(orderItem22);
            OrderItems.Add(orderItem31);

            base.SaveChanges();
        }


        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
