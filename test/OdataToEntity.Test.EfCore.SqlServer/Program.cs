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
            await new PLNull(new PLNull_DbFixtureInitDb()).Expand(0, false);
        }
    }
}