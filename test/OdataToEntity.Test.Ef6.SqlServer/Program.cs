using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdataToEntity.Test.Ef6.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            new ProcedureTest().ResetDb_get().GetAwaiter().GetResult();
            //new SelectTest(new DbFixtureInitDb()).ApplyGroupBySkip().GetAwaiter().GetResult();
        }
    }
}
