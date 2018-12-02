using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.ServiceModel;

namespace OdataToEntity.Test.Model
{
    internal static class TestHelper
    {
        public static Cache.OeQueryCache GetQueryCache(Db.OeDataAdapter dataAdapter) => new Cache.OeQueryCache();
    }
}

namespace OdataToEntity.Test.WcfService
{
    public sealed class OrderServiceBehaviorAttribute : OdataWcfServiceBehaviorAttribute
    {
        public OrderServiceBehaviorAttribute() : base(typeof(Model.OrderDataAdapter))
        {
        }

        protected override OdataWcfService CreateOdataWcfService(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            return new OrderService(dataAdapter, edmModel);
        }
    }

    [OrderServiceBehavior]
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public sealed class OrderService : OdataWcfService
    {
        public OrderService(OeDataAdapter dataAdapter, IEdmModel edmModel) : base(dataAdapter, edmModel)
        {
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            using (var host = new ServiceHost(typeof(OrderService), new Uri("net.tcp://localhost:5000/OdataWcfService")))
            {
                var binding = new NetTcpBinding();
                host.AddServiceEndpoint(typeof(IOdataWcf), binding, String.Empty);
                host.Open();

                do
                    Console.WriteLine("Close server press escape to exit");
                while (Console.ReadKey().Key != ConsoleKey.Escape);
            }
        }
    }
}
