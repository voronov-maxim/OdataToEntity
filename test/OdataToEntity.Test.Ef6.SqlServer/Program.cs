using System.Linq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.Ef6.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var ctx = new OrderEf6Context(true);
            var query = ctx.Customers.GroupJoin(ctx.Orders,
                c => new { c.Country, Id = c.Id },
                o => new { Country = o.CustomerCountry, Id = o.CustomerId },
                (c, o) => new { Customer = c, Order = o.DefaultIfEmpty() })
                .SelectMany(z => z.Order, (c, o) => new { Customer = c.Customer, Order = o })
                .GroupBy(g => g.Customer, (c, o) => new { Customer = c, Order = o })
                .ToArray();

            //EfCore.SqlServer.PerformanceCacheTest.RunTest(100);
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).Table(0).GetAwaiter().GetResult();
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).ExpandExpandFilter(0, false).GetAwaiter().GetResult();
        }
    }
}
