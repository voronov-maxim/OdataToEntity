using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;

namespace OdataToEntity.Test.Ef6.SqlServer
{
    public sealed class OrderEf6Context : DbContext
    {
        static OrderEf6Context()
        {
            Database.SetInitializer<OrderEf6Context>(null);
        }
        public OrderEf6Context(bool useRelationalNulls) : base(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;")
        {
            base.Configuration.AutoDetectChangesEnabled = false;
            base.Configuration.LazyLoadingEnabled = false;
            base.Configuration.ProxyCreationEnabled = false;
            base.Configuration.UseDatabaseNullSemantics = useRelationalNulls;
            base.Configuration.ValidateOnSaveEnabled = false;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<Order>().HasMany(p => p.ShippingAddresses).WithRequired().HasForeignKey(s => s.OrderId);
            modelBuilder.Entity<Customer>().HasMany(p => p.CustomerShippingAddresses).WithRequired().HasForeignKey(s => new { s.CustomerCountry, s.CustomerId });
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerShippingAddress> CustomerShippingAddress { get; set; }
        public DbSet<ManyColumns> ManyColumns { get; set; }
        public DbSet<ManyColumnsView> ManyColumnsView { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ShippingAddress> ShippingAddresses { get; set; }

        public static String GenerateDatabaseName() => "dummy";
        [Description("dbo.GetOrders")]
        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        public void ResetDb() => throw new NotImplementedException();
        public void ResetManyColumns() => throw new NotImplementedException();
        [DbFunction("dbo", "ScalarFunction")]
        public int ScalarFunction() => throw new NotImplementedException();
        [DbFunction("dbo", "ScalarFunctionWithParameters")]
        public int ScalarFunctionWithParameters(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        [DbFunction(".", "TableFunction")]
        public IEnumerable<Order> TableFunction() => throw new NotImplementedException();
        [DbFunction(".", "TableFunctionWithParameters")]
        public IEnumerable<Order> TableFunctionWithParameters(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
    }
}
