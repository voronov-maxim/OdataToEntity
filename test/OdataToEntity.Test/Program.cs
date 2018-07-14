using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var ctx = new Model.OrderContext(Model.OrderContextOptions.Create(true, "zzz"));
            var zzz = ctx.Customers.GroupJoin(ctx.Orders,
                c => new { c.Country, Id = (int?)c.Id },
                o => new { Country = o.AltCustomerCountry, Id = o.AltCustomerId },
                (c, o) => new { Customer = c, Order = o.DefaultIfEmpty() })
                .SelectMany(z => z.Order, (c, o) => new { c.Customer, o }).ToArray();

            new NC_PLNull(new NC_PLNull_DbFixtureInitDb()).ExpandExpandFilter(0, false).GetAwaiter().GetResult();
        }
    }
}
