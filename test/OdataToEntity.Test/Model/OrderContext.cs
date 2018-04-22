using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OdataToEntity.Test.Model
{
    public sealed partial class OrderContext : DbContext
    {
        public OrderContext(DbContextOptions options) : base(options)
        {
            base.ChangeTracker.AutoDetectChangesEnabled = false;
            base.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().HasKey(c => new { c.Country, c.Id });
            base.OnModelCreating(modelBuilder);
        }
        public static String GenerateDatabaseName() => Guid.NewGuid().ToString();

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
        public void ResetManyColumns() => throw new NotImplementedException();
        [DbFunction("ScalarFunction", Schema = "dbo")]
        public int ScalarFunction() => Orders.Count();
        [DbFunction(Schema = "dbo")]
        public int ScalarFunctionWithParameters(int? id, String name, OrderStatus? status) => Orders.Where(o => o.Id == id || o.Name.Contains(name) || o.Status == status).Count();
        [DbFunction("TableFunction")]
        public IEnumerable<Order> TableFunction() => Orders;
        [DbFunction]
        public IEnumerable<Order> TableFunctionWithParameters(int? id, String name, OrderStatus? status) => Orders.Where(o => (o.Id == id) || EF.Functions.Like(o.Name, "%" + name + "%") || (o.Status == status));

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ManyColumns> ManyColumns { get; set; }
        public DbSet<ManyColumnsView> ManyColumnsView { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
