using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            return Create<OrderContext>(useRelationalNulls, databaseName);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls, String databaseName) where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}