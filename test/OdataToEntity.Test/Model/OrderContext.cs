using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderContext : DbContext
    {
        private OrderContext(DbContextOptions options) : base(options)
        {
        }

        public static OrderContext Create(String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseInMemoryDatabase(databaseName);

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
