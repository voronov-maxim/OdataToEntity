using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    public static class OrderContextOptions
    {
        public static EdmModel BuildDbEdmModel(IEdmModel _, bool useRelationalNulls)
        {
            var orderDataAdapter = new OeEfCoreDataAdapter<OrderContext>(Create(useRelationalNulls)) { IsDatabaseNullHighestValue = true };
            IEdmModel orderEdmModel = orderDataAdapter.BuildEdmModelFromEfCoreModel();
            var order2DataAdapter = new OeEfCoreDataAdapter<Order2Context>(Create<Order2Context>(useRelationalNulls)) { IsDatabaseNullHighestValue = true };
            return order2DataAdapter.BuildEdmModelFromEfCoreModel(orderEdmModel);
        }
        public static DbContextOptions Create(bool useRelationalNulls)
        {
            return Create<OrderContext>(useRelationalNulls);
        }
        public static DbContextOptions Create<T>(bool useRelationalNulls) where T : DbContext
        {
            Npgsql.NpgsqlConnection.GlobalTypeMapper.MapComposite<StringList>("dbo.string_list");

            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseNpgsql(@"Host=localhost;Port=5432;Database=OdataToEntity;Pooling=true;User Id=mvoronov", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
    }
}