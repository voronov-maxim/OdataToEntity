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
        private static async Task Execute<T>(String request, Object requestData, Func<OrderContext, IEnumerable<T>> fromDbFunc)
        {
            var fixture = new DbFixtureInitDb();
            fixture.Initalize();

            var parser = new OeParser(new Uri("http://dummy/"), fixture.OeDataAdapter, fixture.EdmModel);
            var responseStream = new MemoryStream();

            var requestUri = new Uri(@"http://dummy/" + request);
            if (requestData == null)
                await parser.ExecuteGetAsync(requestUri, OeRequestHeaders.Default, responseStream, CancellationToken.None);
            else
            {
                String data = JsonConvert.SerializeObject(requestData);
                var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                await parser.ExecutePostAsync(requestUri, OeRequestHeaders.Default, requestStream, responseStream, CancellationToken.None);
            }

            var reader = new ResponseReader(fixture.EdmModel, fixture.DbDataAdapter.EntitySetMetaAdapters);
            responseStream.Position = 0;
            T[] fromOe = reader.ReadFeed<T>(responseStream).ToArray();

            T[] fromDb;
            using (var orderContext = (OrderContext)fixture.DbDataAdapter.CreateDataContext())
                fromDb = fromDbFunc(orderContext).ToArray();

            var settings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            String jsonOe = JsonConvert.SerializeObject(fromOe, settings);
            String jsonDb = JsonConvert.SerializeObject(fromDb, settings);

            Console.WriteLine(requestUri);
            Assert.Equal(jsonDb, jsonOe);
        }
        [Fact]
        public async Task GetOrders_get()
        {
            String request = "GetOrders(name='Order 1',id=1,status=null)";
            await Execute<Order>(request, null, c => c.GetOrders(1, "Order 1", null));
        }
        [Fact]
        public async Task GetOrders_post()
        {
            String request = "GetOrders";
            var requestData = new { id = 1, name = "Order 1", status = "Unknown" };
            await Execute<Order>(request, requestData, c => c.GetOrders(1, "Order 1", null));
        }
    }
}
