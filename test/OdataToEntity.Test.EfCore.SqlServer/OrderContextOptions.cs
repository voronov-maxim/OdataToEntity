using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static EdmModel BuildDbEdmModel(bool useRelationalNulls)
        {
            var orderDataAdapter = new OeEfCoreDataAdapter<OrderContext>(Create(useRelationalNulls));
            IEdmModel orderEdmModel = orderDataAdapter.BuildEdmModel();
            var order2DataAdapter = new OeEfCoreDataAdapter<Order2Context>(Create<Order2Context>(useRelationalNulls));
            return order2DataAdapter.BuildEdmModel(orderEdmModel);
        }
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
        public static DbContextOptions CreateClientEvaluationWarning(bool useRelationalNulls)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls))
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.QueryClientEvaluationWarning));
            return optionsBuilder.Options;
        }
    }
}