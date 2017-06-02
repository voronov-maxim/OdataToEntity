using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().HasKey(c => new { c.Country, c.Id });
            base.OnModelCreating(modelBuilder);
        }

        [Description("dbo.GetOrders")]
        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status)
        {
            if (id == null && name == null && status == null)
                return Orders;

            if (id != null)
                return Orders.Where(o => o.Id == id);

            if (name != null)
                return Orders.Where(o => o.Name.Contains(name));

            if (status != null)
                return Orders.Where(o => o.Status == status);

            return Enumerable.Empty<Order>();
        }
        public void ResetDb() => throw new NotImplementedException();
    }
}
