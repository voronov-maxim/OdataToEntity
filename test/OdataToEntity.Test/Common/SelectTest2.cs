using Microsoft.OData;
using Microsoft.OData.Edm;
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
        [Fact]
        public async Task BoundFunctionCollection()
        {
            List<int> expectedResult;
            using (var dbContext = Fixture.CreateContext())
                expectedResult = dbContext.OrderItems.Where(i =>
                    (i.Order.Name == "Order 1" || i.Order.Name == "Order 2") && i.Order.Customer.Sex == Model.Sex.Female)
                    .Select(i => i.Id).ToList();

            Db.OeDataAdapter dataAdapter = Fixture.OeEdmModel.GetDataAdapter(Fixture.OeEdmModel.EntityContainer);
            String request = $"Customers/$filter(Sex eq 'Female')/BoundFunctionCollection(orderNames=['Order 1','Order 2'])?$expand=Customer,Items&$select=Name";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            List<Object> fromOe = new ResponseReader(edmModel).Read(response).Cast<Object>().ToList();
            Assert.Equal(expectedResult, fromOe.SelectMany(c => (IEnumerable<dynamic>)((dynamic)c).Items).Select(i => (int)i.Id).OrderBy(id => id));
        }
        [Fact]
        public async Task BoundFunctionSingle()
        {
            List<int> expectedResult;
            using (var dbContext = Fixture.CreateContext())
                expectedResult = dbContext.OrderItems.Where(i =>
                    (i.Order.Name == "Order 1" || i.Order.Name == "Order 2") && i.Order.Customer.Country == "RU" && i.Order.Customer.Id == 1)
                    .Select(i => i.Id).ToList();

            Db.OeDataAdapter dataAdapter = Fixture.OeEdmModel.GetDataAdapter(Fixture.OeEdmModel.EntityContainer);
            String request = $"Customers('RU',1)/{dataAdapter.DataContextType.Namespace}.BoundFunctionSingle(orderNames=['Order 1','Order 2'])?$expand=Customer,Items&$select=Name";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            List<Object> fromOe = new ResponseReader(edmModel).Read(response).Cast<Object>().ToList();
            Assert.Equal(expectedResult, fromOe.SelectMany(c => (IEnumerable<dynamic>)((dynamic)c).Items).Select(i => (int)i.Id).OrderBy(id => id));
        }
        [Fact]
        internal async Task CountExpandNested()
        {
            String request = "Orders?$expand=Items($count=true)&orderby=Id";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var actualCounts = new List<long>();
            var reader = new ResponseReader(edmModel);
            foreach (dynamic order in reader.Read(response))
            {
                var navigationProperty = (IEnumerable)order.Items;
                actualCounts.Add(reader.GetNavigationInfo(navigationProperty).Count.Value);
            }

            List<long> expectedCounts;
            using (var dbContext = Fixture.CreateContext())
                expectedCounts = dbContext.Orders.OrderBy(o => o.Id).Select(i => (long)i.Items.Count()).ToList();

            Assert.Equal(expectedCounts, actualCounts);
        }
        [Fact]
        public async Task CountQueryParameter()
        {
            String request = "Orders?&$count=true&$top=1";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var reader = new ResponseReader(edmModel);
            Assert.Single(reader.Read(response).Cast<Object>());

            int? expectedCount;
            using (var dbContext = Fixture.CreateContext())
                expectedCount = dbContext.Orders.Count();

            Assert.Equal(expectedCount, reader.ResourceSet.Count);
        }
        [Fact]
        public async Task CountQueryParameterFilter()
        {
            String request = "OrderItems?$filter=OrderId eq 1&$count=true&$top=1";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var reader = new ResponseReader(edmModel);
            Assert.Single(reader.Read(response).Cast<Object>());

            int? expectedCount;
            using (var dbContext = Fixture.CreateContext())
                expectedCount = dbContext.OrderItems.Count(i => i.OrderId == 1);

            Assert.Equal(expectedCount, reader.ResourceSet.Count);
        }
        [Fact]
        public async Task NavigationNextPageLink()
        {
            String request = "Orders?$expand=Items($filter=Count gt 0 or Count eq null;$orderby=Id;$count=true)&$orderby=Id&$count=true";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.OeEdmModel.GetEdmModel(odataUri.Path);
            var requestUri = new Uri(odataUri.ServiceRoot, request);

            var expectedResponse = new MemoryStream();
            var expectedParser = new OeParser(odataUri.ServiceRoot, edmModel);
            await expectedParser.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, expectedResponse, CancellationToken.None).ConfigureAwait(false);
            expectedResponse.Position = 0;

            var exprectedReader = new ResponseReader(edmModel);
            List<Object> expectedResult = exprectedReader.Read(expectedResponse).Cast<Object>().ToList();

            List<Object> fromOe = await OrdersCountItemsCount(Fixture, request, i => i.Count == null || i.Count > 0, 1, true);
            TestHelper.Compare(expectedResult, fromOe, null);
        }
        [Fact]
        public async Task NextPageLink()
        {
            String request = "OrderItems?$orderby=Id&$count=true";

            const int pageSize = 2;
            var pageNextLinkModelBoundBuilder = new PageNextLinkModelBoundBuilder(Fixture.OeEdmModel, Fixture.IsSqlite);
            Query.OeModelBoundProvider modelBoundProvider = pageNextLinkModelBoundBuilder.BuildProvider(pageSize, false);
            OeParser parser = Fixture.CreateParser(request, modelBoundProvider);
            var uri = new Uri(parser.BaseUri, request);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
                var reader = new ResponseReader(parser.EdmModel);
                response.Position = 0;

                List<Object> result = reader.Read(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, pageSize);
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

            TestHelper.Compare(fromDb, fromOe, null);
        }
        internal static async Task<List<Object>> OrdersCountItemsCount(DbFixture fixture, String request, Func<Model.OrderItem, bool> orderItemPredicate,
            int maxPageSize, bool navigationNextLink)
        {
            Query.OeModelBoundProvider modelBoundProvider = fixture.ModelBoundProvider;
            if (modelBoundProvider == null)
            {
                var modelBoundProviderBuilder = new PageNextLinkModelBoundBuilder(fixture.OeEdmModel, fixture.IsSqlite);
                modelBoundProvider = modelBoundProviderBuilder.BuildProvider(maxPageSize, navigationNextLink);
            }

            OeParser parser = fixture.CreateParser(request, modelBoundProvider);
            Uri uri = new Uri(parser.BaseUri, request);

            int expectedCount;
            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault.SetMaxPageSize(maxPageSize), response, CancellationToken.None).ConfigureAwait(false);
                response.Position = 0;

                var reader = new ResponseReader(parser.EdmModel);
                List<Object> result = reader.Read(response).Cast<Object>().ToList();
                fromOe.AddRange(result);

                if (maxPageSize > 0)
                    Assert.InRange(result.Count, 0, maxPageSize);

                var navigationPropertyParser = new OeParser(parser.BaseUri, parser.EdmModel, fixture.ModelBoundProvider);
                foreach (dynamic order in result)
                {
                    using (var dbContext = fixture.CreateContext())
                    {
                        String orderName = order.Name;
                        expectedCount = dbContext.OrderItems.Where(i => i.Order.Name == orderName).ToList().Where(orderItemPredicate).Count();
                    }

                    var navigationProperty = (IEnumerable)order.Items;
                    ResponseReader.NavigationInfo info = reader.GetNavigationInfo(navigationProperty);

                    if (!navigationNextLink && !uri.OriginalString.Contains("$skiptoken="))
                        Assert.Equal(expectedCount, info.Count);

                    var navigationPropertyResponse = new MemoryStream();
                    await navigationPropertyParser.ExecuteGetAsync(info.NextPageLink, OeRequestHeaders.JsonDefault, navigationPropertyResponse, CancellationToken.None).ConfigureAwait(false);
                    navigationPropertyResponse.Position = 0;

                    var navigationPropertyReader = new ResponseReader(parser.EdmModel);
                    foreach (dynamic orderItem in navigationPropertyReader.Read(navigationPropertyResponse))
                        order.Items.Add(orderItem);

                    if (navigationNextLink)
                        Assert.Equal(expectedCount, navigationPropertyReader.ResourceSet.Count);
                }

                if (count < 0)
                    count = reader.ResourceSet.Count.GetValueOrDefault();

                uri = reader.ResourceSet.NextPageLink;
            }
            while (uri != null);

            using (var dbContext = fixture.CreateContext())
                expectedCount = dbContext.Orders.Count();
            Assert.Equal(expectedCount, count);

            return fromOe;
        }
    }
}
