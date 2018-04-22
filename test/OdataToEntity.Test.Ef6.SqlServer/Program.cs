using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.Ef6.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            EfCore.SqlServer.PerformanceCacheTest.RunTest(100);
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).Table(0).GetAwaiter().GetResult();
        }
    }
}
