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
            modelBuilder.HasDefaultSchema("dbo");
            modelBuilder.Entity<Customer>().HasKey(c => new { c.Country, c.Id });
            modelBuilder.Entity<ShippingAddress>().HasKey(s => new { s.OrderId, s.Id });
            modelBuilder.Entity<CustomerShippingAddress>().HasKey(t => new { t.CustomerCountry, t.CustomerId, t.ShippingAddressOrderId, t.ShippingAddressId });
            modelBuilder.Entity<OrderItemsView>().HasNoKey();
            base.OnModelCreating(modelBuilder);
        }

        [Db.OeBoundFunction(collectionFunctionName: "BoundFunctionCollection", singleFunctionName: "BoundFunctionSingle")]
        public static IEnumerable<Order> BoundFunction(Db.OeBoundFunctionParameter<Customer, Order> boundParameter, IEnumerable<String> orderNames)
        {
            OrderContext orderContext = boundParameter.CreateDataContext<OrderContext>();

            IQueryable<Customer> customers = boundParameter.ApplyFilter(orderContext.Customers, orderContext);
            IQueryable<Order> orders = customers.SelectMany(c => c.Orders).Where(o => orderNames.Contains(o.Name));
            IQueryable result = boundParameter.ApplySelect(orders, orderContext);
            List<Order> orderList = boundParameter.Materialize(result).ToListAsync().GetAwaiter().GetResult();

            boundParameter.CloseDataContext(orderContext);
            return orderList;
        }
        public static String GenerateDatabaseName() => Guid.NewGuid().ToString();
        [Description("dbo.GetOrders")]
        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status)
        {
            if (id == null && name == null && status == null)
                return Orders;

            if (id != null)
                return Orders.AsQueryable().Where(o => o.Id == id);

            if (name != null)
                return Orders.AsQueryable().Where(o => o.Name.Contains(name));

            if (status != null)
                return Orders.AsQueryable().Where(o => o.Status == status);

            return Enumerable.Empty<Order>();
        }
        [Description("ResetDb()")]
        public void ResetDb() => throw new NotImplementedException();
        [Description("ResetManyColumns()")]
        public void ResetManyColumns() => throw new NotImplementedException();
        [DbFunction("ScalarFunction", Schema = "dbo")]
        public int ScalarFunction() => Orders.Count();
        [DbFunction(Schema = "dbo")]
        public int ScalarFunctionWithParameters(int? id, String name, OrderStatus? status) => Orders.AsQueryable().Where(o => o.Id == id || o.Name.Contains(name) || o.Status == status).Count();
        [Description("TableFunction()")]
        public IEnumerable<Order> TableFunction() => Orders;
        [Description("TableFunctionWithCollectionParameter()")]
        public IEnumerable<String> TableFunctionWithCollectionParameter(IEnumerable<String> string_list) => string_list;
        [Description("TableFunctionWithParameters()")]
        public IEnumerable<Order> TableFunctionWithParameters(int? id, String name, OrderStatus? status) => Orders.AsQueryable().Where(o => (o.Id == id) || EF.Functions.Like(o.Name, "%" + name + "%") || (o.Status == status));

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerShippingAddress> CustomerShippingAddress { get; set; }
        public DbSet<ManyColumns> ManyColumns { get; set; }
        public DbSet<ManyColumnsView> ManyColumnsView { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderItemsView> OrderItemsView { get; set; }
        public DbSet<ShippingAddress> ShippingAddresses { get; set; }
    }

    public sealed class Order2Context : DbContext
    {
        public Order2Context(DbContextOptions options) : base(options)
        {
            base.ChangeTracker.AutoDetectChangesEnabled = false;
            base.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().HasKey(c => new { c.Country, c.Id });
            modelBuilder.Entity<ShippingAddress>().HasKey(s => new { s.OrderId, s.Id });
            modelBuilder.Entity<CustomerShippingAddress>().HasKey(t => new { t.CustomerCountry, t.CustomerId, t.ShippingAddressOrderId, t.ShippingAddressId });

            modelBuilder.Entity<CustomerOrdersCount>().HasNoKey();

            modelBuilder.Entity<Order>().HasData(
                new Order() { Id = 1, Name = "Order from Order2 context", AltCustomerCountry = null, AltCustomerId = null, CustomerCountry = "AL", CustomerId = 42, Status = OrderStatus.Cancelled }
                );

            modelBuilder.Entity<ManyColumns2>().HasData(
                new ManyColumns2() { Column01 = 123456 }
                );

            modelBuilder.Entity<Customer>().HasData(
                new Customer() { Id = 42, Country = "AL", Name = "Dua Lipa" }
                );
        }

        public DbSet<ManyColumns2> ManyColumns2 { get; set; }
        public DbSet<Order> Orders2 { get; set; }
        public DbSet<Customer> Customer2 { get; set; }
    }
}
