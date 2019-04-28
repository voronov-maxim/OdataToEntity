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
        static async Task Main()
        {
            //EfCore.SqlServer.PerformanceCacheTest.RunTest(100);
            await new RDBNull(new RDBNull_DbFixtureInitDb()).ReferencedModels();
        }
    }
}
