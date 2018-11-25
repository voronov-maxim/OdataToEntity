using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OdataToEntity.EfCore;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
            return optionsBuilder.Options;
        }
        public static DbContextOptions CreateClientEvaluationWarning(bool useRelationalNulls, String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrderContext>();
            optionsBuilder = optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls))
                .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.QueryClientEvaluationWarning));
            return optionsBuilder.Options;
        }
    }

    public sealed class OrderDbDataAdapter : OeEfCoreDataAdapter<OrderContext>
    {
        private readonly bool _useRelationalNulls;

        public OrderDbDataAdapter(bool allowCache, bool useRelationalNulls, String databaseName) : base(new Cache.OeQueryCache(allowCache))
        {
            _useRelationalNulls = useRelationalNulls;
        }

        public override Object CreateDataContext()
        {
            return new OrderContext(OrderContextOptions.Create(_useRelationalNulls, null));
        }
    }
}