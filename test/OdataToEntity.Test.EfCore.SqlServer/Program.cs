using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            new ProcedureTest().GetOrders_post().GetAwaiter().GetResult();
            //PerformanceCacheTest.RunTest(100);

            //var fixture = new DbFixtureInitDb();
            //new SelectTest(fixture).TopSkip().GetAwaiter().GetResult();
        }
    }
}