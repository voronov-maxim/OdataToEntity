using Microsoft.OData.Client;
using ODataClient.Default;
using System;

namespace OdataToEntity.Test.AspClient
{
    internal sealed class NC_PLNull : SelectTest
    {
        public NC_PLNull() : base(new DbFixtureInitDb())
        {
        }
    }

    internal sealed class NC_PLNull_ManyColumns : ManyColumnsTest
    {
        public NC_PLNull_ManyColumns() : base(new ManyColumnsFixtureInitDb())
        {
        }
    }

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
            DbFixtureInitDb.RunTest(new NC_PLNull()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new NC_PLNull_ManyColumns()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new ProcedureTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
