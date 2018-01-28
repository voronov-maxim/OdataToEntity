using Microsoft.OData;
using OdataToEntity.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public partial class SelectTest
    {
        [Fact]
        internal async Task CountExpandNested()
        {
            String request = "Orders?$expand=Items($count=true)&orderby=Id";

            ODataUri odataUri = Fixture.ParseUri(request);
            var parser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel);
            var uri = new Uri(odataUri.ServiceRoot, request);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var actualCounts = new List<long>();
            var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
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
            var parser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
            reader.Read(response).Cast<Object>().Single();

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
            var parser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel);

            var response = new MemoryStream();
            await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
            response.Position = 0;

            var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
            reader.Read(response).Cast<Object>().Single();

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
            var parser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel) { NavigationNextLink = true, PageSize = 1 };
            var requestUri = new Uri(odataUri.ServiceRoot, request);
            Uri uri = requestUri;

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
                response.Position = 0;

                var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
                List<Object> result = reader.Read(response).Cast<Object>().ToList();
                Assert.InRange(result.Count, 0, parser.PageSize);
                fromOe.AddRange(result);

                var navigationPropertyParser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel);
                foreach (dynamic order in result)
                {
                    var navigationProperty = (IEnumerable)order.Items;
                    ODataResourceSetBase resourceSet = reader.GetResourceSet(navigationProperty);

                    var navigationPropertyResponse = new MemoryStream();
                    await navigationPropertyParser.ExecuteGetAsync(resourceSet.NextPageLink, OeRequestHeaders.JsonDefault, navigationPropertyResponse, CancellationToken.None).ConfigureAwait(false);
                    navigationPropertyResponse.Position = 0;

                    var navigationPropertyReader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
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

            parser.NavigationNextLink = false;
            parser.PageSize = 0;

            var exprectedResponse = new MemoryStream();
            var expectedParser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel);
            await expectedParser.ExecuteGetAsync(requestUri, OeRequestHeaders.JsonDefault, exprectedResponse, CancellationToken.None).ConfigureAwait(false);
            exprectedResponse.Position = 0;

            var exprectedReader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
            List<Object> expectedResult = exprectedReader.Read(exprectedResponse).Cast<Object>().ToList();

            TestHelper.Compare(expectedResult, fromOe, null);
        }
        [Fact]
        public async Task NextPageLink()
        {
            String request = "OrderItems?$orderby=Id&$count=true";

            ODataUri odataUri = Fixture.ParseUri(request);
            var parser = new OeParser(odataUri.ServiceRoot, Fixture.OeDataAdapter, Fixture.EdmModel) { PageSize = 2 };
            var uri = new Uri(odataUri.ServiceRoot, request);

            long count = -1;
            var fromOe = new List<Object>();
            do
            {
                var response = new MemoryStream();
                await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None).ConfigureAwait(false);
                var reader = new ResponseReader(Fixture.EdmModel, Fixture.OeDataAdapter.EntitySetMetaAdapters);
                response.Position = 0;

                List<Object> result = reader.Read(response).Cast<Object>().ToList();
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

            TestHelper.Compare(fromDb, fromOe, null);
        }
    }
}
