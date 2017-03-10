namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static void Main2(string[] args)
        {
            PerformanceCacheTest.RunTest(100);

            //var fixture = new DbFixtureInitDb();
            //new SelectTest(fixture).TopSkip().GetAwaiter().GetResult();
        }
    }
}