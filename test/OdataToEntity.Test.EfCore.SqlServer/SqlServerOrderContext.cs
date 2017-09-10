using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed partial class OrderContext : DbContext
    {
        public static OrderContext Create(String dummy)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            return new OrderContext(optionsBuilder.Options);
        }
        public static String GenerateDatabaseName() => "dummy";
    }
}
