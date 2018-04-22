using Microsoft.OData.Client;
using ODataClient.Default;
using System;
using System.ServiceModel;

namespace OdataToEntity.Test.WcfClient
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
        private static readonly WcfClientInterceptor _interceptor = new WcfClientInterceptor(new NetTcpBinding(), RemoteAddress);
        public const String RemoteAddress = "net.tcp://localhost:5000/OdataWcfService";

        private static Container ContainerFactory()
        {
            var container = new Container(new Uri("http://dummy")) { MergeOption = MergeOption.OverwriteChanges };
            _interceptor.AttachToContext(container);
            return container;
        }

        static void Main(string[] args)
        {
            DbFixtureInitDb.ContainerFactory = ContainerFactory;

            DbFixtureInitDb.RunTest(new AspClient.BatchTest()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new NC_PLNull()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new NC_PLNull_ManyColumns()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new AspClient.ProcedureTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
