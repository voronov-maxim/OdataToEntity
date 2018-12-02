using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.OData.Edm;
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
            SelectTestDefinition[] testDefinitions = SelectTestDefinition.GetSelectTestDefinitions();

            //warming-up
            var dataAdapter = new OrderDataAdapter(false, false, null);
            var dbContext = (DbContext)dataAdapter.CreateDataContext();
            foreach (SelectTestDefinition testDefinition in testDefinitions)
                testDefinition.ExecutorDb(dataAdapter, dbContext);
            dataAdapter.CloseDataContext(dbContext);

            PerformanceCacheOeTest(testDefinitions, testCount, true);
            PerformanceCacheOeTest(testDefinitions, testCount, false);
            PerformanceCacheDbTest(testDefinitions, testCount);
        }
        private static void PerformanceCacheDbTest(SelectTestDefinition[] testDefinitions, int testCount)
        {
            var pool = new DbContextPool<OrderContext>(OrderContextOptions.Create(true, null));
            var dataAdapter = new OrderDataAdapter(false, false, null);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < testCount; i++)
                foreach (SelectTestDefinition testDefinition in testDefinitions)
                {
                    OrderContext dbContext = pool.Rent();
                    testDefinition.ExecutorDb(dataAdapter, dbContext);
                    pool.Return(dbContext);
                }
            stopWatch.Stop();
            Console.WriteLine("Entity Framework " + stopWatch.Elapsed);
        }
        private static void PerformanceCacheOeTest(SelectTestDefinition[] testDefinitions, int testCount, bool allowCache)
        {
            var dataAdapter = new OeEfCoreDataAdapter<OrderContext>(OrderContextOptions.Create(true, null), new Cache.OeQueryCache(allowCache));
            IEdmModel edmModel = dataAdapter.BuildEdmModel();
            var parser = new OeParser(new Uri("http://dummy"), edmModel);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (int i = 0; i < testCount; i++)
                foreach (SelectTestDefinition testDefinition in testDefinitions)
                {
                    var uri = new Uri("http://dummy/" + testDefinition.Request);
                    using (var response = new MemoryStream())
                        parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).GetAwaiter().GetResult();
                }
            stopWatch.Stop();
            Console.WriteLine("OdataToEntity cache = " + allowCache + " " + stopWatch.Elapsed);
        }
    }
}
