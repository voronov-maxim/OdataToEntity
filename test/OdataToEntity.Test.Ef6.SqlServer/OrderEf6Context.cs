using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;

namespace OdataToEntity.Test.Ef6.SqlServer
{
    public sealed class OrderEf6Context  : DbContext
    {
        public OrderEf6Context() : base(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;")
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ManyColumns> ManyColumns { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        [Description("dbo.GetOrders")]
        public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
        public void ResetDb() => throw new NotImplementedException();
        public void ResetManyColumns() => throw new NotImplementedException();
    }
}
