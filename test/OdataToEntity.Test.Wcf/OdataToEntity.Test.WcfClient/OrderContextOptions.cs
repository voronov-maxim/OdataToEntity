using Microsoft.EntityFrameworkCore;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static DbContextOptions Create(bool useRelationalNulls)
        {
            return Create<OrderContext>(useRelationalNulls);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls) where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}