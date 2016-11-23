using ODataClient.Default;
using OdataToEntity.Test;
using System;

namespace OdataToEntityCore.AspClient
{
    class Program
    {
        private static Container CreateContainer()
        {
            return new Container(new Uri("http://localhost:5000/api"));
        }

        static void Main(String[] args)
        {
            DbFixture.ContainerFactory = CreateContainer;

            //new SelectTest().SelectName().Wait();

            DbFixture.RunTest(new BatchTest()).GetAwaiter().GetResult();
            DbFixture.RunTest(new SelectTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
