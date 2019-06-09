using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static EdmModel BuildDbEdmModel(bool useRelationalNulls, bool isDatabaseNullHighestValue)
        {
            var orderDataAdapter = new OeEfCoreDataAdapter<OrderContext>(Create(useRelationalNulls)) { IsDatabaseNullHighestValue = isDatabaseNullHighestValue };
            IEdmModel orderEdmModel = orderDataAdapter.BuildEdmModelFromEfCoreModel();
            var order2DataAdapter = new OeEfCoreDataAdapter<Order2Context>(Create<Order2Context>(useRelationalNulls)) { IsDatabaseNullHighestValue = isDatabaseNullHighestValue };
            return order2DataAdapter.BuildEdmModelFromEfCoreModel(orderEdmModel);
        }
        public static DbContextOptions<OrderContext> Create(bool useRelationalNulls)
        {
            return Create<OrderContext>(useRelationalNulls);
        }
        public static DbContextOptions<T> Create<T>(bool useRelationalNulls) where T : DbContext
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