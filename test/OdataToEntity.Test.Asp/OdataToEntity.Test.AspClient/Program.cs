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

            //new ProcedureTest().GetOrders_id_get();
            DbFixtureInitDb.RunTest(new BatchTest()).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new SelectTest(new DbFixtureInitDb())).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new ManyColumnsTest(new ManyColumnsFixtureInitDb())).GetAwaiter().GetResult();
            DbFixtureInitDb.RunTest(new ProcedureTest()).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async static Task TestNextLink()
        {
            System.Threading.Thread.Sleep(500);

            var container = DbFixtureInitDb.CreateContainer();
            var query = (DataServiceQuery)container.Orders.Expand(o => o.Items).OrderBy(o => o.Id);

            var orders = new List<Order>();
            DataServiceQueryContinuation<Order> continuation = null;
            for (var response = (QueryOperationResponse<Order>) await query.ExecuteAsync(); response != null;
                continuation = response.GetContinuation(), response = continuation == null ? null : (QueryOperationResponse<Order>)await container.ExecuteAsync(continuation))
                foreach (var order in response)
                {
                    DataServiceQueryContinuation itemsContinuation = response.GetContinuation(order.Items);
                    while (itemsContinuation != null)
                    {
                        QueryOperationResponse itemsResponse = await container.LoadPropertyAsync(order, nameof(order.Items), itemsContinuation);
                        itemsContinuation = itemsResponse.GetContinuation();
                    }
                    orders.Add(order);
                }
        }
    }
}
