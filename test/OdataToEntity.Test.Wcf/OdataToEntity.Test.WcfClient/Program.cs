using Microsoft.OData.Client;
using ODataClient.OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace OdataToEntity.Test.WcfClient
{
    internal sealed class WcfDbFixtureInitDb : DbFixtureInitDb
    {
        internal protected override async Task<IList> ExecuteOeViaHttpClient<T, TResult>(QueryParameters<T, TResult> parameters, long? resourceSetCount)
        {
            WcfService.OdataWcfQuery result = await Program.Interceptor.Get(parameters.RequestUri);

            var responseReader = new ResponseReader(base.EdmModel);

            IList fromOe;
            if (typeof(TResult) == typeof(Object))
                fromOe = responseReader.Read<T>(result.Content).Cast<Object>().ToList();
            else
                fromOe = responseReader.Read<TResult>(result.Content).Cast<Object>().ToList();

            if (resourceSetCount != null)
                Xunit.Assert.Equal(resourceSetCount, responseReader.ResourceSet.Count);

            return fromOe;
        }
    }

    internal sealed class NC_PLNull : SelectTest
    {
        public NC_PLNull() : base(new WcfDbFixtureInitDb())
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
        internal static readonly WcfClientInterceptor Interceptor = new WcfClientInterceptor(new NetTcpBinding(), RemoteAddress);
        public const String RemoteAddress = "net.tcp://localhost:5000/OdataWcfService";

        private static Container ContainerFactory()
        {
            var container = new Container(new Uri("http://dummy")) { MergeOption = MergeOption.OverwriteChanges };
            Interceptor.AttachToContext(container);
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
