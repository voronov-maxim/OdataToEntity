using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.Test;
using OdataToEntity.Test.Model;
using System;
using System.ServiceModel;

namespace OdataToEntity.Test.WcfService
{
    [ServiceContract]
    public interface IOrderDb
    {
        [OperationContract]
        void Init();
        [OperationContract]
        void Reset();
    }

    public sealed class OrderServiceBehaviorAttribute : OdataWcfServiceBehaviorAttribute
    {
        public OrderServiceBehaviorAttribute() : base(typeof(OrderOeDataAdapter))
        {
        }

        protected override OdataWcfService CreateOdataWcfService(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            return new OrderService(dataAdapter, edmModel);
        }
    }

    [OrderServiceBehavior]
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public sealed class OrderService : OdataWcfService, IOrderDb
    {
        public OrderService(OeDataAdapter dataAdapter, IEdmModel edmModel) : base(dataAdapter, edmModel)
        {
        }

        public void Init()
        {
            OrderContext dbContext = null;
            try
            {
                dbContext = (OrderContext)base.DataAdapter.CreateDataContext();
                dbContext.InitDb();
            }
            finally
            {
                if (dbContext != null)
                    base.DataAdapter.CloseDataContext(dbContext);
            }
        }
        public void Reset()
        {
            ((OrderOeDataAdapter)base.DataAdapter).ResetDatabase();
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
                host.AddServiceEndpoint(typeof(IOrderDb), binding, String.Empty);
                host.Open();

                do
                    Console.WriteLine("Close server press escape to exit");
                while (Console.ReadKey().Key != ConsoleKey.Escape);
            }
        }
    }
}
