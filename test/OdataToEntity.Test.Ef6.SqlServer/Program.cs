using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

namespace OdataToEntity.Test.Ef6.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //using (var ctx = new OrderContext())
            //{
            //    var query = ctx.Orders.Where(o => o.Id == 1).Include(o => o.Customer);
            //    var zzz = query.ToArray();
            //}
            new SelectTest(new DbFixtureInitDb()).ApplyGroupBySkip().GetAwaiter().GetResult();
        }
    }
}
