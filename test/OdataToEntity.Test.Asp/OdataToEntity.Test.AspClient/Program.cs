using ODataClient.Default;
using OdataToEntity.Test;
using System;

namespace OdataToEntity.Test.AspClient
{
    class Program
    {
        private static Container CreateContainer()
        {
            return new Container(new Uri("http://localhost:5000/api"));
        }

        static void Main(String[] args)
        {
            DbFixtureInitDb.ContainerFactory = CreateContainer;

            //new ProcedureTest().GetOrders_id_get();
            DbFixtureInitDb.RunTest(new BatchTest()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new SelectTest(new DbFixtureInitDb())).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new ManyColumnsTest(new ManyColumnsFixtureInitDb())).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new ProcedureTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
