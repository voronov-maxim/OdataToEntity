using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }

}