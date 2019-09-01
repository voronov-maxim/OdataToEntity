using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static async Task Main()
        {
            //PerformanceCacheTest.RunTest(100);
            var ctx = new OrderContext(OrderContextOptions.Create(true));
            var zzz = ctx.Orders.AsQueryable()
                .Where(o => o.Status == OrderStatus.Unknown).GroupBy(o => o.Name).Select(g => new { Name = g.Key, cnt = g.GroupBy(o => o.Id).Count() }).ToArray();


            await new PLNull(new PLNull_DbFixtureInitDb()).ApplyGroupByFilter(0);
        }
    }
}