using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Newtonsoft.Json.Linq;
using OdataToEntity.Test.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public partial class SelectTest
    {
        //https://msdn.microsoft.com/en-us/library/dn725245.aspx
        //[Fact]
        internal async Task CountExpandNested()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=Items($count=true)",
                Expression = t => t.Include(o => o.Items),
            };
            await Fixture.Execute(parameters);

            JObject responseJObject;
            using (var stream = new MemoryStream())
            {
                ODataUri odataUri = Fixture.ParseUri(parameters.RequestUri);
                var parser = new OeParser(new Uri("http://dummy/"), Fixture.OeDataAdapter, Fixture.EdmModel);
                await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                {
                    string responseStr = await reader.ReadToEndAsync();
                    responseJObject = JObject.Parse(responseStr);
                }
            }

            var jArray = (JArray)responseJObject["value"];
            var actualCounts = jArray.Select(o => (int?)o["Items@odata.count"]);

            int?[] expectedCounts = null;
            using (var dbContext = Fixture.CreateContext())
                expectedCounts = dbContext.Orders.Select(i => (int?)i.Items.Count()).ToArray();

            Assert.Equal(expectedCounts, actualCounts);
        }
        [Fact]
        public async Task CountQueryParameter()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=Items&$count=true&$top=1",
                Expression = t => t.Include(o => o.Items).Take(1),
            };
            await Fixture.Execute(parameters);

            JObject responseJObject;
            using (var stream = new MemoryStream())
            {
                ODataUri odataUri = Fixture.ParseUri(parameters.RequestUri);
                var parser = new OeParser(new Uri("http://dummy/"), Fixture.OeDataAdapter, Fixture.EdmModel);
                await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                {
                    String responseStr = await reader.ReadToEndAsync();
                    responseJObject = JObject.Parse(responseStr);
                }
            }

            var actualCount = (int?)responseJObject["@odata.count"];

            int? expectedCount = null;
            using (var dbContext = Fixture.CreateContext())
                expectedCount = dbContext.Orders.Count();

            Assert.Equal(expectedCount, actualCount);
        }
        [Fact]
        public async Task CountQueryParameterFilter()
        {
            var parameters = new QueryParameters<OrderItem>()
            {
                RequestUri = "OrderItems?$filter=OrderId eq 1&$count=true&$top=1",
                Expression = t => t.Where(i => i.OrderId == 1).Take(1),
            };
            await Fixture.Execute(parameters);

            JObject responseJObject;
            using (var stream = new MemoryStream())
            {
                ODataUri odataUri = Fixture.ParseUri(parameters.RequestUri);
                var parser = new OeParser(new Uri("http://dummy/"), Fixture.OeDataAdapter, Fixture.EdmModel);
                await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, stream, CancellationToken.None);
                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                {
                    String responseStr = await reader.ReadToEndAsync();
                    responseJObject = JObject.Parse(responseStr);
                }
            }

            var actualCount = (int?)responseJObject["@odata.count"];

            int? expectedCount = null;
            using (var dbContext = Fixture.CreateContext())
                expectedCount = dbContext.OrderItems.Where(i => i.OrderId == 1).Count();

            Assert.Equal(expectedCount, actualCount);
        }
        [Fact]
        public async Task NextPageLink()
        {
            var parser = new OeParser(new Uri("http://dummy"), Fixture.OeDataAdapter, Fixture.EdmModel) { PageSize = 2 };
            var uri = new Uri("http://dummy/OrderItems?$orderby=Id&$count=true");

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None);
                var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
                response.Position = 0;

                List<Object> result = reader.ReadFeed(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, parser.PageSize);
                fromOe.AddRange(result);

                if (count < 0)
                    count = reader.ResourceSet.Count.GetValueOrDefault();
                uri = reader.ResourceSet.NextPageLink;
            }
            while (uri != null);
            Assert.Equal(count, fromOe.Count);

            IList fromDb;
            using (var context = Fixture.CreateContext())
                fromDb = context.OrderItems.OrderBy(i => i.Id).ToList();

            DbFixture.Compare(fromDb, fromOe);
        }
        [Fact]
        public async Task NavigationNextPageLink()
        {
            var parser = new OeParser(new Uri("http://dummy"), Fixture.OeDataAdapter, Fixture.EdmModel) { NavigationNextLink = true, PageSize = 2 };
            var requestUri = new Uri("http://dummy/Orders?$expand=Items($filter=Count gt 0 or Count eq null;$orderby=Id;$count=true)&$orderby=Id&$count=true");
            var uri = requestUri;

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None);
                response.Position = 0;

                var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
                List<Object> result = reader.ReadFeed(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, parser.PageSize);
                fromOe.AddRange(result);

                foreach (Order order in result)
                {
                    ODataResourceSetBase resourceSet = reader.GetResourceSet(order.Items);
                    var navigationPropertyResponse = new MemoryStream();
                    var navigationPropertyParser = new OeParser(new Uri("http://dummy"), Fixture.OeDataAdapter, Fixture.EdmModel);
                    await navigationPropertyParser.ExecuteGetAsync(resourceSet.NextPageLink, OeRequestHeaders.JsonDefault, navigationPropertyResponse, CancellationToken.None);
                    navigationPropertyResponse.Position = 0;

                    var navigationPropertyReader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
                    foreach (OrderItem orderItem in navigationPropertyReader.ReadFeed(navigationPropertyResponse))
                        order.Items.Add(orderItem);
                }

                if (count < 0)
                    count = reader.ResourceSet.Count.GetValueOrDefault();
                uri = reader.ResourceSet.NextPageLink;
            }
            while (uri != null);
            Assert.Equal(count, fromOe.Count);

            parser.NavigationNextLink = false;
            parser.PageSize = 0;

            var response2 = new MemoryStream();
            var parser2 = new OeParser(new Uri("http://dummy"), Fixture.OeDataAdapter, Fixture.EdmModel);
            await parser2.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, response2, CancellationToken.None);
            response2.Position = 0;

            var reader2 = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
            List<Object> result2 = reader2.ReadFeed(response2).Cast<Object>().ToList();

            DbFixture.Compare(result2, fromOe);
        }
    }
}
