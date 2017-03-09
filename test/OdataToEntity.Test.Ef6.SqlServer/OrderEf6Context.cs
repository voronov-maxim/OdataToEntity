using OdataToEntity.Test.Model;
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
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
