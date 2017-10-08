using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    public sealed partial class OrderContext : DbContext
    {
        public static OrderContext Create(String dummy)
        {
            return new OrderContext(CreateOptions());
        }
        internal static DbContextOptions CreateOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true;");
            return optionsBuilder.Options;
        }

        public static String GenerateDatabaseName() => "dummy";
    }
}

namespace OdataToEntity.Test
{
    public sealed partial class OrderOeDataAdapter
    {
        public EdmModel BuildEdmModel()
        {
            return this.BuildEdmModelFromEfCorePgSqlModel("dbo");
        }
    }
}
