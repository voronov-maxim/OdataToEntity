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
            //new SelectTest(new DbFixtureInitDb()).ApplyGroupByAggregateFilter().GetAwaiter().GetResult();
            new BatchTest().Add().GetAwaiter().GetResult();
        }
    }
}
