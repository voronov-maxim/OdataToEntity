using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public static OrderContext Create(String databaseName)
        {
            return new OrderContext();
        }
        public static String GenerateDatabaseName()
        {
            return "dummy";
        }
    }
}
