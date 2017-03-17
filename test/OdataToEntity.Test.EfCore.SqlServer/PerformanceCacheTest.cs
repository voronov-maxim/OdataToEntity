using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OdataToEntity.Test.EfCore.SqlServer
{
    public static class PerformanceCacheTest
    {
        public static void RunTest(int testCount)
        {
            SelectTestDefinition[] testDefinitions = TestHelper.GetSelectTestDefinitions();

            //warming-up
            foreach (SelectTestDefinition testDefinition in testDefinitions)
                using (var dbContext = new OrderContext())
                    testDefinition.ExecutorDb(dbContext);

            PerformanceCacheOeTest(testDefinitions, testCount, true);
            PerformanceCacheOeTest(testDefinitions, testCount, false);
            PerformanceCacheDbTest(testDefinitions, testCount);
        }
        private static void PerformanceCacheDbTest(SelectTestDefinition[] testDefinitions, int testCount)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < testCount; i++)
                foreach (SelectTestDefinition testDefinition in testDefinitions)
                    using (var dbContext = new OrderContext())
                        testDefinition.ExecutorDb(dbContext);
            stopWatch.Stop();
            Console.WriteLine("Entity Framework " + stopWatch.Elapsed);
        }
        private static void PerformanceCacheOeTest(SelectTestDefinition[] testDefinitions, int testCount, bool cache)
        {
            var dataAdapter = new OeEfCoreDataAdapter<OrderContext>(new Db.OeQueryCache() { AllowCache = cache });
            IEdmModel edmModel = dataAdapter.BuildEdmModel();
            var parser = new OeParser(new Uri("http://dummy"), dataAdapter, edmModel);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < testCount; i++)
                foreach (SelectTestDefinition testDefinition in testDefinitions)
                {
                    var uri = new Uri("http://dummy/" + testDefinition.Request);
                    using (var response = new MemoryStream())
                        parser.ExecuteQueryAsync(uri, OeRequestHeaders.Default, response, CancellationToken.None).GetAwaiter().GetResult();
                }
            stopWatch.Stop();
            Console.WriteLine("OdataToEntity cache = " + cache + " " + stopWatch.Elapsed);
        }
    }
}
