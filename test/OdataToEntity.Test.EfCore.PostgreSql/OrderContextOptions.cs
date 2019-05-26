using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;

namespace OdataToEntity.Test.Model
{
    public static class OrderContextOptions
    {
        public static EdmModel BuildDbEdmModel(bool useRelationalNulls)
        {
            var orderDataAdapter = new OrderDataAdapter(false, useRelationalNulls);
            IEdmModel orderEdmModel = orderDataAdapter.BuildEdmModel();
            var order2DataAdapter = new Order2DataAdapter(false, useRelationalNulls);
            return order2DataAdapter.BuildEdmModel(orderEdmModel);
        }
        public static DbContextOptions Create(bool useRelationalNulls)
        {
            return Create<OrderContext>(useRelationalNulls);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls) where T : DbContext
        {
            Npgsql.NpgsqlConnection.GlobalTypeMapper.MapComposite<OdataToEntity.EfCore.StringList>("dbo.string_list");

            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}