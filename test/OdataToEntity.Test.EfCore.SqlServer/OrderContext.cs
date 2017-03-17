using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Test.Model
{
    public sealed class OrderContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public static OrderContext Create(String databaseName) => new OrderContext();
        public static String GenerateDatabaseName() => "dummy";

        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        public void ResetDb() => throw new NotImplementedException();
    }
}
