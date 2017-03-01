extern alias lq2db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

namespace OdataToEntity.Test.Linq2Db
{
    class Program
    {
        static void Main(string[] args)
        {
            new SelectTest(new DbFixtureInitDb()).ApplyGroupByAggregateFilter().GetAwaiter().GetResult();
            //new BatchTest().Delete().GetAwaiter().GetResult();

            //using (var ctx = new OdataToEntityDB("OdataToEntity"))
            //{
            //    var zzz = ctx.Orders.Where(o => o.Status == lq2db::OdataToEntity.Test.Model.OrderStatus.Shipped)
            //        .GroupBy(o => new Tuple<string>(o.Name))
            //        .Select(g => new Tuple<Tuple<string>, int>(g.Key, g.Select(Param_3 => Param_3.Id).Distinct().Count())).ToArray();
            //    //Param_2.Select(Param_3 => Param_3.Id).Distinct().Count()))
            //}
        }
    }
}
