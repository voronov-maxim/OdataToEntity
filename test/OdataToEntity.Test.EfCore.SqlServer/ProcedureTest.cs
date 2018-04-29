using Newtonsoft.Json;
using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class ProcedureTest
    {
        private static async Task<T[]> Execute<T>(String request, Object requestData, Func<OrderContext, IEnumerable<T>> fromDbFunc)
        {
            var fixture = new NC_RDBNull_DbFixtureInitDb();
            await fixture.Initalize();

            var parser = new OeParser(new Uri("http://dummy/"), fixture.OeDataAdapter, fixture.EdmModel);
            var responseStream = new MemoryStream();

            var requestUri = new Uri(@"http://dummy/" + request);
            if (requestData == null)
                await parser.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, responseStream, CancellationToken.None);
            else
            {
                String data = JsonConvert.SerializeObject(requestData);
                var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                await parser.ExecutePostAsync(requestUri, OeRequestHeaders.JsonDefault, requestStream, responseStream, CancellationToken.None);
            }

            var reader = new ResponseReader(fixture.EdmModel, fixture.DbDataAdapter);
            responseStream.Position = 0;
            T[] fromOe;
            if (typeof(T) == typeof(int))
            {
                String count = new StreamReader(responseStream).ReadToEnd();
                fromOe = count == "" ? null : new T[] { (T)(Object)int.Parse(count) };
            }
            else
                fromOe = reader.Read<T>(responseStream).ToArray();

            if (fromDbFunc == null)
                return fromOe;

            T[] fromDb;
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
                fromDb = fromDbFunc(orderContext).ToArray();

            var settings = new JsonSerializerSettings()
            {
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffff",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);

            Console.WriteLine(requestUri);
            Assert.Equal(jsonDb, jsonOe);

            return fromOe;
        }
        [Fact]
        public async Task GetOrders_id_get()
        {
            String request = "dbo.GetOrders(name='Order 1',id=1,status=null)";
            await Execute<Order>(request, null, c => c.GetOrders(1, "Order 1", null));
        }
        [Fact]
        public async Task GetOrders_id_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = 1, name = "Order 1", status = "Unknown" };
            await Execute<Order>(request, requestData, c => c.GetOrders(1, "Order 1", null));
        }
        [Fact]
        public async Task GetOrders_name_get()
        {
            String request = "dbo.GetOrders(name='Order',id=null,status=null)";
            await Execute<Order>(request, null, c => c.GetOrders(null, "Order", null));
        }
        [Fact]
        public async Task GetOrders_name_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = (int?)null, name = "Order", status = (OrderStatus?)null };
            await Execute<Order>(request, requestData, c => c.GetOrders(null, "Order", null));
        }
        [Fact]
        public async Task GetOrders_status_get()
        {
            String request = "dbo.GetOrders(name=null,id=null,status=OdataToEntity.Test.Model.OrderStatus'Processing')";
            await Execute<Order>(request, null, c => c.GetOrders(null, null,  OrderStatus.Processing));
        }
        [Fact]
        public async Task GetOrders_status_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = (int?)null, name = (String)null, status = OrderStatus.Processing.ToString() };
            await Execute<Order>(request, requestData, c => c.GetOrders(null, null, OrderStatus.Processing));
        }
        [Fact]
        public async Task ResetDb_get()
        {
            String request = "ResetDb";
            await Execute<int>(request, null, null);

            var fixture = new NC_RDBNull_DbFixtureInitDb();
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
            {
                int count = orderContext.Categories.Count() +
                    orderContext.Customers.Count() +
                    orderContext.Orders.Count() +
                    orderContext.OrderItems.Count();
                Assert.Equal(0, count);
            }
        }
        [Fact]
        public async Task ResetDb_post()
        {
            String request = "ResetDb";
            await Execute<int>(request, "", null);

            var fixture = new NC_RDBNull_DbFixtureInitDb();
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
            {
                int count = orderContext.Categories.Count() +
                    orderContext.Customers.Count() +
                    orderContext.Orders.Count() +
                    orderContext.OrderItems.Count();
                Assert.Equal(0, count);
            }
        }
        [Fact]
        public async Task ScalarFunction_get()
        {
            String request = "dbo.ScalarFunction";
            int[] result = await Execute<int>(request, null, null);

            var fixture = new NC_RDBNull_DbFixtureInitDb();
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
            {
                int count = orderContext.ScalarFunction();
                Assert.Equal(count, result[0]);
            }
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_get()
        {
            String request = "dbo.ScalarFunctionWithParameters(name='Order 1',id=1,status=null)";
            int[] result = await Execute<int>(request, null, null);

            var fixture = new NC_RDBNull_DbFixtureInitDb();
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
            {
                int count = orderContext.ScalarFunctionWithParameters(1, "Order 1", null);
                Assert.Equal(count, result[0]);
            }
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_post()
        {
            String request = "dbo.ScalarFunctionWithParameters";
            var requestData = new { id = (int?)1, name = "Order 1", status = (OrderStatus?)null };
            int[] result = await Execute<int>(request, requestData, null);

            var fixture = new NC_RDBNull_DbFixtureInitDb();
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
            {
                int count = orderContext.ScalarFunctionWithParameters(1, "Order 1", null);
                Assert.Equal(count, result[0]);
            }
        }
        [Fact]
        public async Task TableFunction_get()
        {
            String request = "TableFunction";
            await Execute(request, null, c => c.TableFunction());
        }
        [Fact]
        public async Task TableFunctionWithParameters_get()
        {
            String request = "TableFunctionWithParameters(name='Order 1',id=1,status=null)";
            await Execute(request, null, c => c.TableFunctionWithParameters(1, "Order 1", null));
        }
        [Fact]
        public async Task TableFunctionWithParameters_post()
        {
            String request = "TableFunctionWithParameters";
            var requestData = new { id = (int?)1, name = "Order 1", status = (OrderStatus?)null };
            await Execute(request, requestData, c => c.TableFunctionWithParameters(1, "Order 1", null));
        }
    }
}
