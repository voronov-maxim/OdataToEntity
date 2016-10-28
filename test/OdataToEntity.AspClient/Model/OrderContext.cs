using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderContext : DbContext
    {
        private OrderContext(DbContextOptions options) : base(options)
        {
        }

        public static OrderContext Create()
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
            
            return new OrderContext(optionsBuilder.Options);
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
