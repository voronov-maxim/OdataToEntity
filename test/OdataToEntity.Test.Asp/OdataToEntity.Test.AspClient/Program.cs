using Microsoft.OData.Client;
using ODataClient.Default;
using ODataClient.OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OdataToEntity.Test.AspClient
{
    class Program
    {
        private static Container CreateContainer()
        {
            return new Container(new Uri("http://localhost:5000/api")) { MergeOption = MergeOption.OverwriteChanges };
        }

        static void Main(String[] args)
        {
            DbFixtureInitDb.ContainerFactory = CreateContainer;

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
