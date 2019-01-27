using Microsoft.OData.Edm;
using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;

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

        [Db.OeBoundFunction(CollectionFunctionName = "BoundFunctionCollection", SingleFunctionName = "BoundFunctionSingle")]
        public static IEnumerable<Order> BoundFunction(Db.OeBoundFunctionParameter<Customer, Order> boundParameter, IEnumerable<String> orderNames)
        {
            using (var orderContext = new OrderEf6Context(true))
            {
                IQueryable<Customer> customers = boundParameter.ApplyFilter(orderContext.Customers, orderContext);
                IQueryable<Order> orders = customers.SelectMany(c => c.Orders).Where(o => orderNames.Contains(o.Name));

                IQueryable result = boundParameter.ApplySelect(orders, orderContext);
                List<Order> orderList = boundParameter.Materialize(result).ToList().GetAwaiter().GetResult();
                return orderList;
            }
        }
        public static String GenerateDatabaseName() => "dummy";
        [Description("dbo.GetOrders")]
        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        [Description("ResetDb()")]
        public void ResetDb() => throw new NotImplementedException();
        [Description("ResetManyColumns()")]
        public void ResetManyColumns() => throw new NotImplementedException();
        [DbFunction("dbo", "ScalarFunction")]
        public int ScalarFunction() => throw new NotImplementedException();
        [DbFunction("dbo", "ScalarFunctionWithParameters")]
        public int ScalarFunctionWithParameters(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        [DbFunction(".", "TableFunction")]
        public IEnumerable<Order> TableFunction() => throw new NotImplementedException();
        [Description("TableFunctionWithCollectionParameter()")]
        public IEnumerable<String> TableFunctionWithCollectionParameter(IEnumerable<String> string_list) => string_list;
        [DbFunction(".", "TableFunctionWithParameters")]
        public IEnumerable<Order> TableFunctionWithParameters(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
    }

    public sealed class Order2Ef6Context : DbContext
    {
        static Order2Ef6Context()
        {
            Database.SetInitializer<Order2Ef6Context>(null);
        }
        public Order2Ef6Context(bool useRelationalNulls) : base(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;")
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

        public DbSet<Customer> Customers2 { get; set; }
        public DbSet<Order> Orders2 { get; set; }
    }
}
