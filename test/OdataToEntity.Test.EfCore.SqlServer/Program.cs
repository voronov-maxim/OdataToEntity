using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            PerformanceCacheTest.RunTest(100);
            //new AC_RDBNull(new AC_RDBNull_DbFixtureInitDb()).Table(0).GetAwaiter().GetResult();
        }
    }
}