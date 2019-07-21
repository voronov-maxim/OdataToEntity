using Microsoft.OData;
using Microsoft.OData.UriParser;
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
        private static async Task<Object[]> Execute<T>(String request, Object requestData, Func<OrderContext, IEnumerable<T>> fromDbFunc)
        {
            var fixture = new RDBNull_DbFixtureInitDb();
            await fixture.Initalize().ConfigureAwait(false);

            var responseStream = new MemoryStream();

            OeParser parser = fixture.CreateParser(request, null);
            ODataUri odataUri = fixture.ParseUri(request);
            var requestUri = new Uri(parser.BaseUri, request);
            if (requestData == null)
                await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, null, responseStream, CancellationToken.None).ConfigureAwait(false);
            else
            {
                String data = fixture.SerializeRequestData(requestData);
                var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, requestStream, responseStream, CancellationToken.None).ConfigureAwait(false);
            }

            ODataPath path = OeParser.ParsePath(fixture.DbEdmModel, parser.BaseUri, requestUri);
            var reader = new ResponseReader(fixture.DbEdmModel.GetEdmModel(path), fixture.ServiceProvider);
            responseStream.Position = 0;
            Object[] fromOe;
            if (typeof(T) == typeof(int))
            {
                String count = new StreamReader(responseStream).ReadToEnd();
                fromOe = count == "" ? null : new Object[] { int.Parse(count) };
            }
            else if (typeof(T) == typeof(String))
            {
                String json = new StreamReader(responseStream).ReadToEnd();
                var jobject = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(json);
                var jarray = (Newtonsoft.Json.Linq.JArray)jobject["value"];
                fromOe = jarray.Select(j => (String)j).ToArray();
            }
            else
                fromOe = reader.Read(responseStream).Cast<Object>().ToArray();

            if (fromDbFunc == null)
                return fromOe;

            T[] fromDb;
            using (OrderContext orderContext = fixture.CreateContext())
                fromDb = fromDbFunc(orderContext).ToArray();

            TestHelper.Compare(fromDb, fromOe, null);
            return fromOe;
        }
        [Fact]
        public async Task GetOrders_id_get()
        {
            String request = "dbo.GetOrders(name='Order 1',id=1,status=null)";
            await Execute<Order>(request, null, c => c.GetOrders(1, "Order 1", null)).ConfigureAwait(false);
        }
        [Fact]
        public async Task GetOrders_id_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = 1, name = "Order 1", status = "Unknown" };
            await Execute<Order>(request, requestData, c => c.GetOrders(1, "Order 1", null)).ConfigureAwait(false);
        }
        [Fact]
        public async Task GetOrders_name_get()
        {
            String request = "dbo.GetOrders(name='Order',id=null,status=null)";
            await Execute<Order>(request, null, c => c.GetOrders(null, "Order", null)).ConfigureAwait(false);
        }
        [Fact]
        public async Task GetOrders_name_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = (int?)null, name = "Order", status = (OrderStatus?)null };
            await Execute<Order>(request, requestData, c => c.GetOrders(null, "Order", null)).ConfigureAwait(false);
        }
        [Fact]
        public async Task GetOrders_status_get()
        {
            String request = "dbo.GetOrders(name=null,id=null,status='Processing')";
            await Execute<Order>(request, null, c => c.GetOrders(null, null, OrderStatus.Processing)).ConfigureAwait(false);
        }
        [Fact]
        public async Task GetOrders_status_post()
        {
            String request = "dbo.GetOrders";
            var requestData = new { id = (int?)null, name = (String)null, status = OrderStatus.Processing.ToString() };
            await Execute<Order>(request, requestData, c => c.GetOrders(null, null, OrderStatus.Processing)).ConfigureAwait(false);
        }
        [Fact]
        public async Task ResetDb_get()
        {
            String request = "ResetDb";
            await Execute<int>(request, null, null).ConfigureAwait(false);

            var fixture = new RDBNull_DbFixtureInitDb();
            using (OrderContext orderContext = fixture.CreateContext())
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
            await Execute<int>(request, "", null).ConfigureAwait(false);

            var fixture = new RDBNull_DbFixtureInitDb();
            using (OrderContext orderContext = fixture.CreateContext())
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
            Object[] result = await Execute<int>(request, null, null).ConfigureAwait(false);

            var fixture = new RDBNull_DbFixtureInitDb();
            using (OrderContext orderContext = fixture.CreateContext())
            {
                int count = orderContext.ScalarFunction();
                Assert.Equal(count, (int)result[0]);
            }
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_get()
        {
            String request = "dbo.ScalarFunctionWithParameters(name='Order 1',id=1,status=null)";
            Object[] result = await Execute<int>(request, null, null).ConfigureAwait(false);

            var fixture = new RDBNull_DbFixtureInitDb();
            using (OrderContext orderContext = fixture.CreateContext())
            {
                int count = orderContext.ScalarFunctionWithParameters(1, "Order 1", null);
                Assert.Equal(count, (int)result[0]);
            }
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_post()
        {
            String request = "dbo.ScalarFunctionWithParameters";
            var requestData = new { id = (int?)1, name = "Order 1", status = (OrderStatus?)null };
            Object[] result = await Execute<int>(request, requestData, null).ConfigureAwait(false);

            var fixture = new RDBNull_DbFixtureInitDb();
            using (OrderContext orderContext = fixture.CreateContext())
            {
                int count = orderContext.ScalarFunctionWithParameters(1, "Order 1", null);
                Assert.Equal(count, (int)result[0]);
            }
        }
        [Fact]
        public async Task TableFunction_get()
        {
            String request = "TableFunction";
            await Execute(request, null, c => c.TableFunction()).ConfigureAwait(false);
        }
        [Fact]
        public async Task TableFunctionWithCollectionParameter_get()
        {
            String request = "TableFunctionWithCollectionParameter(string_list=['Foo','Bar','Baz'])";
            await Execute(request, null, c => c.TableFunctionWithCollectionParameter(new[] { "Foo", "Bar", "Baz" })).ConfigureAwait(false);
        }
        [Fact]
        public async Task TableFunctionWithParameters_get()
        {
            String request = "TableFunctionWithParameters(name='Order 1',id=1,status=null)";
            await Execute(request, null, c => c.TableFunctionWithParameters(1, "Order 1", null)).ConfigureAwait(false);
        }
        [Fact]
        public async Task TableFunctionWithParameters_post()
        {
            String request = "TableFunctionWithParameters";
            var requestData = new { id = (int?)1, name = "Order 1", status = (OrderStatus?)null };
            await Execute(request, requestData, c => c.TableFunctionWithParameters(1, "Order 1", null)).ConfigureAwait(false);
        }
    }
}
