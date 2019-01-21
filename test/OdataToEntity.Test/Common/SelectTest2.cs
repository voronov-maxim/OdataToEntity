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
                    i.Order.Customer.Name == "Natasha" ||
                    i.Order.Customer.Name == "Ivan" ||
                    i.Order.Customer.Name == "Sasha").Select(i => i.Id).ToList();

            Db.OeDataAdapter dataAdapter = Fixture.EdmModel.GetDataAdapter(Fixture.EdmModel.EntityContainer);
            String request = $"Orders/{dataAdapter.DataContextType.Namespace}.BoundFunctionCollection(customerNames=['Natasha','Ivan','Sasha'])";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);
            var uri = new Uri(odataUri.ServiceRoot, request);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            List<Object> fromOe = new ResponseReader(edmModel).Read(response).Cast<Object>().ToList();
            Assert.Equal(expectedResult, fromOe.Select(i => (int)((dynamic)i).Id).OrderBy(id => id));
        }
        [Fact]
        public async Task BoundFunctionSingle()
        {
            int orderId;
            List<int> expectedResult;
            using (var dbContext = Fixture.CreateContext())
            {
                orderId = dbContext.Orders.Single(i => i.Name == "Order 1").Id;
                expectedResult = dbContext.OrderItems.Where(i => i.OrderId == orderId).Select(i => i.Id).ToList();
            }

            Db.OeDataAdapter dataAdapter = Fixture.EdmModel.GetDataAdapter(Fixture.EdmModel.EntityContainer);
            String request = $"Orders({orderId})/{dataAdapter.DataContextType.Namespace}.BoundFunctionSingle(customerNames=['Natasha','Ivan','Sasha'])";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);
            var uri = new Uri(odataUri.ServiceRoot, request);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            List<Object> fromOe = new ResponseReader(edmModel).Read(response).Cast<Object>().ToList();
            Assert.Equal(expectedResult, fromOe.Select(i => (int)((dynamic)i).Id).OrderBy(id => id));
        }
        [Fact]
        internal async Task CountExpandNested()
        {
            String request = "Orders?$expand=Items($count=true)&orderby=Id";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);
            var uri = new Uri(odataUri.ServiceRoot, request);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var actualCounts = new List<long>();
            var reader = new ResponseReader(edmModel);
            foreach (dynamic order in reader.Read(response))
            {
                var navigationProperty = (IEnumerable)order.Items;
                actualCounts.Add(reader.GetResourceSet(navigationProperty).Count.Value);
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
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
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
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
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
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);
            var requestUri = new Uri(odataUri.ServiceRoot, request);
            Uri uri = requestUri;
            OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(1).SetNavigationNextLink(true);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None).ConfigureAwait(false);
                response.Position = 0;

                var reader = new ResponseReader(edmModel);
                List<Object> result = reader.Read(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, requestHeaders.MaxPageSize);
                fromOe.AddRange(result);

                var navigationPropertyParser = new OeParser(odataUri.ServiceRoot, edmModel);
                foreach (dynamic order in result)
                {
                    var navigationProperty = (IEnumerable)order.Items;
                    ODataResourceSetBase resourceSet = reader.GetResourceSet(navigationProperty);

                    var navigationPropertyResponse = new MemoryStream();
                    await navigationPropertyParser.ExecuteGetAsync(resourceSet.NextPageLink, OeRequestHeaders.JsonDefault, navigationPropertyResponse, CancellationToken.None).ConfigureAwait(false);
                    navigationPropertyResponse.Position = 0;

                    var navigationPropertyReader = new ResponseReader(edmModel);
                    foreach (dynamic orderItem in navigationPropertyReader.Read(navigationPropertyResponse))
                        order.Items.Add(orderItem);

                    Assert.Equal(navigationPropertyReader.ResourceSet.Count, order.Items.Count);
                }

                if (count < 0)
                    count = reader.ResourceSet.Count.GetValueOrDefault();

                uri = reader.ResourceSet.NextPageLink;
            }
            while (uri != null);

            Assert.Equal(count, fromOe.Count);

            var exprectedResponse = new MemoryStream();
            var expectedParser = new OeParser(odataUri.ServiceRoot, edmModel);
            await expectedParser.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, exprectedResponse, CancellationToken.None).ConfigureAwait(false);
            exprectedResponse.Position = 0;

            var exprectedReader = new ResponseReader(edmModel);
            List<Object> expectedResult = exprectedReader.Read(exprectedResponse).Cast<Object>().ToList();

            TestHelper.Compare(expectedResult, fromOe, Fixture.MetadataProvider, null);
        }
        [Fact]
        public async Task NextPageLink()
        {
            String request = "OrderItems?$orderby=Id&$count=true";

            ODataUri odataUri = Fixture.ParseUri(request);
            IEdmModel edmModel = Fixture.EdmModel.GetEdmModel(odataUri.Path);
            var parser = new OeParser(odataUri.ServiceRoot, edmModel);
            var uri = new Uri(odataUri.ServiceRoot, request);
            OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(2);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None).ConfigureAwait(false);
                var reader = new ResponseReader(edmModel);
                response.Position = 0;

                List<Object> result = reader.Read(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, requestHeaders.MaxPageSize);
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

            TestHelper.Compare(fromDb, fromOe, Fixture.MetadataProvider, null);
        }
    }
}
