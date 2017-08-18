using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            var category1 = new Category()
            {
                Id = 1,
                Name = "clothes",
                ParentId = null,
                DateTime = DateTime.Parse("2016-07-04T19:10:10.8237573+03:00")
            };
            var category2 = new Category()
            {
                Id = 2,
                Name = "unknown",
                ParentId = null
            };
            var category3 = new Category()
            {
                Id = 3,
                Name = "hats",
                ParentId = 1,
                DateTime = DateTime.Parse("2016-07-04T19:10:10.8237573+03:00")
            };
            var category4 = new Category()
            {
                Id = 4,
                Name = "jackets",
                ParentId = 1,
                DateTime = DateTime.Parse("2016-07-04T19:10:10.8237573+03:00")
            };
            var category5 = new Category()
            {
                Id = 5,
                Name = "baseball cap",
                ParentId = 3,
                DateTime = DateTime.Parse("2000-01-01T00:00:00Z")
            };
            var category6 = new Category()
            {
                Id = 6,
                Name = "sombrero",
                ParentId = 3,
                DateTime = DateTime.Parse("3000-01-01T00:00:00Z")
            };
            var category7 = new Category()
            {
                Id = 7,
                Name = "fur coat",
                ParentId = 4,
                DateTime = DateTime.Parse("2016-07-04T19:10:11.0000000+03:00")
            };
            var category8 = new Category()
            {
                Id = 8,
                Name = "cloak",
                ParentId = 4
            };

            var customer1 = new Customer()
            {
                Address = "Moscow",
                Country = "RU",
                Id = 1,
                Name = "Ivan",
                Sex = Sex.Male
            };
            var customer2 = new Customer()
            {
                Address = "London",
                Country = "EN",
                Id = 1,
                Name = "Natasha",
                Sex = Sex.Female
            };
            var customer3 = new Customer()
            {
                Address = "Tula",
                Country = "RU",
                Id = 2,
                Name = "Sasha",
                Sex = Sex.Female
            };
            var customer4 = new Customer()
            {
                Address = null,
                Country = "UN",
                Id = 1,
                Name = "Unknown",
                Sex = null
            };

            var order1 = new Order()
            {
                Date = DateTimeOffset.Parse("2016-07-04T19:10:10.8237573+03:00"),
                Id = 1,
                Name = "Order 1",
                CustomerCountry = "RU",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order2 = new Order()
            {
                Date = DateTimeOffset.Parse("2016-07-04T19:10:11.0000000+03:00"),
                Id = 2,
                Name = "Order 2",
                CustomerCountry = "EN",
                CustomerId = 1,
                Status = OrderStatus.Processing
            };
            var order3 = new Order()
            {
                AltCustomerCountry = "RU",
                AltCustomerId = 2,
                Date = null,
                Id = 3,
                Name = "Order unknown",
                CustomerCountry = "UN",
                CustomerId = 1,
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
            var orderItem32 = new OrderItem()
            {
                Count = 0,
                Id = 7,
                OrderId = 3,
                Price = 0,
                Product = "{ null }.Sum() == 0"
            };

            Categories.Add(category1);
            Categories.Add(category2);
            Categories.Add(category3);
            Categories.Add(category4);
            Categories.Add(category5);
            Categories.Add(category6);
            Categories.Add(category7);
            Categories.Add(category8);
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
            OrderItems.Add(orderItem32);

            base.SaveChanges();
        }
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().HasKey(c => new { c.Country, c.Id });
            base.OnModelCreating(modelBuilder);
        }

        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        public void ResetDb() => throw new NotImplementedException();

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
