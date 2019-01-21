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

        [Description("BoundFunctionCollection()")]
        public static IEnumerable<OrderItem> BoundFunctionCollection(IEdmModel edmModel, IAsyncEnumerator<Order> orders, IEnumerable<String> customerNames)
        {
            var customerNameSet = new HashSet<String>(customerNames);
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(typeof(OrderEf6Context));
            var orderContext = (OrderEf6Context)dataAdapter.CreateDataContext();
            while (orders.MoveNext().GetAwaiter().GetResult())
            {
                var order = orders.Current;
                Customer customer = orderContext.Customers.Find(new Object[] { order.CustomerCountry, order.CustomerId });
                if (customerNameSet.Contains(customer.Name))
                    foreach (OrderItem orderItem in orderContext.OrderItems.Where(i => i.OrderId == order.Id))
                        yield return orderItem;
            }
        }
        [Description("BoundFunctionSingle()")]
        public static IEnumerable<OrderItem> BoundFunctionSingle(IEdmModel edmModel, Order order, IEnumerable<String> customerNames)
        {
            var customerNameSet = new HashSet<String>(customerNames);
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(typeof(OrderEf6Context));
            var orderContext = (OrderEf6Context)dataAdapter.CreateDataContext();
            Customer customer = orderContext.Customers.Find(new Object[] { order.CustomerCountry, order.CustomerId });
            if (customerNameSet.Contains(customer.Name))
                foreach (OrderItem orderItem in orderContext.OrderItems.Where(i => i.OrderId == order.Id))
                    yield return orderItem;
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
