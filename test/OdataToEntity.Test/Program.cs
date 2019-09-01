using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test
{
    class Program
    {
        static async Task Main()
        {
            //await new PLNull(new PLNull_DbFixtureInitDb()).FilterIn(0);
            var fixture = new PLNull_DbFixtureInitDb();
            var ctx = fixture.CreateContext();
            //ctx.Orders.AsQueryable().Where(o => o.Date.Value.Year == 2016 && o.Date.Value.Month > 3 && o.Date.Value.Day < 20).ToArray();
            var d = DateTimeOffset.Now;
            //ctx.Categories.AsQueryable().Where(o => o.DateTime.Value.Year == 2016).ToArray();
            ctx.Orders.AsQueryable().Where(o => o.Date == DateTimeOffset.Now).ToArray();
        }
    }
}
