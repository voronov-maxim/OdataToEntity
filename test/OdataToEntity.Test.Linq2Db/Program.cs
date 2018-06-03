extern alias lq2db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using OdataToEntityDB = lq2db::OdataToEntity.Test.Model.OdataToEntityDB;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.Linq2Db
{
    class Program
    {
        static void Main(string[] args)
        {
            //EfCore.SqlServer.PerformanceCacheTest.RunTest(100);
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).Table(0).GetAwaiter().GetResult();
        }
    }
}
