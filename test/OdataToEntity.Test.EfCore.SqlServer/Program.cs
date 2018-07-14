using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var ctx = new Model.OrderContext(Model.OrderContextOptions.Create(true, "zzz"));
            //var query = ctx.Customers.GroupJoin(ctx.Orders,
            //    c => new { c.Country, Id = c.Id },
            //    o => new { Country = o.CustomerCountry, Id = o.CustomerId },
            //    (c, o) => new { Customer = c, Order = o.DefaultIfEmpty() })
            //    .SelectMany(z => z.Order, (c, o) => new { Customer = c.Customer, Order = o })
            //    .GroupBy(g => g.Customer, (c, o) => new { Customer = c, Order = o })
            //    .ToArray();

            //var query2 = query.GroupJoin(ctx.OrderItems,
            //    g => g.Item2.Id,
            //    i => i.OrderId,
            //    (g, i) => Tuple.Create(g, i.DefaultIfEmpty()));
            //.SelectMany(z => z.Item2, (g, i) => Tuple.Create(g.Item1, i));
            //var zzz = query2.ToArray();

            //var zzz = ctx.Orders.GroupJoin(
            //    ctx.Orders.SelectMany(o => ctx.OrderItems.Where(i => i.OrderId == o.Id).OrderBy(i => i.Price).Take(2)),
            //    o => o.Id, i => i.OrderId, (o, i) => new { o, i = i.DefaultIfEmpty() })
            //    .SelectMany(z => z.i, (o, i) => new { Order = o.o, Item = i }).OrderBy(z => z.Order.Customer.Name).ToArray();

            //var zzz2 = ctx.Orders.GroupJoin(
            //    ctx.OrderItems.Take(2),
            //    o => o.Id, i => i.OrderId, (o, i) => new { o, i = i.DefaultIfEmpty() })
            //    .SelectMany(z => z.i, (o, i) => new { Order = o.o, Item = i }).OrderBy(z => z.Order.Customer.Name).ToArray();

            //PerformanceCacheTest.RunTest(100);
            new NC_PLNull_ManyColumns(new NC_PLNull_ManyColumnsFixtureInitDb()).Select(1).GetAwaiter().GetResult(); ;
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).SelectName(1).GetAwaiter().GetResult();
        }
    }
}