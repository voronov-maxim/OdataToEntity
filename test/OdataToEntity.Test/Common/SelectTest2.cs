using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    partial class SelectTest
    {
        //https://msdn.microsoft.com/en-us/library/dn725245.aspx
        //[Fact]
        public async Task CountExpandNested()
        {
            var parameters = new QueryParameters<Order>()
            {
                RequestUri = "Orders?$expand=Items($count=true)",
                Expression = t => t.Include(o => o.Items),
            };
            await Fixture.Execute(parameters);

            Newtonsoft.Json.Linq.JObject responseJObject;
            using (var stream = new System.IO.MemoryStream())
            {
                ODataUri odataUri = Fixture.ParseUri(parameters.RequestUri);
                var parser = new OeParser(new Uri("http://dummy/"), Fixture.OeDataAdapter, Fixture.EdmModel);
                await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.Default, stream, CancellationToken.None);
                stream.Position = 0;
                using (var reader = new System.IO.StreamReader(stream))
                {
                    string responseStr = await reader.ReadToEndAsync();
                    responseJObject = Newtonsoft.Json.Linq.JObject.Parse(responseStr);
                }
            }

            var jArray = (Newtonsoft.Json.Linq.JArray)responseJObject["value"];
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
                RequestUri = "Orders?$expand=Items&$count=true",
                Expression = t => t.Include(o => o.Items),
            };
            await Fixture.Execute(parameters);

            Newtonsoft.Json.Linq.JObject responseJObject;
            using (var stream = new System.IO.MemoryStream())
            {
                ODataUri odataUri = Fixture.ParseUri(parameters.RequestUri);
                var parser = new OeParser(new Uri("http://dummy/"), Fixture.OeDataAdapter, Fixture.EdmModel);
                await parser.ExecuteQueryAsync(odataUri, OeRequestHeaders.Default, stream, CancellationToken.None);
                stream.Position = 0;
                using (var reader = new System.IO.StreamReader(stream))
                {
                    string responseStr = await reader.ReadToEndAsync();
                    responseJObject = Newtonsoft.Json.Linq.JObject.Parse(responseStr);
                }
            }

            var actualCount = (int?)responseJObject["@odata.count"];

            int? expectedCount = null;
            using (var dbContext = Fixture.CreateContext())
                expectedCount = dbContext.Orders.Count();

            Assert.Equal(expectedCount, actualCount);
        }
    }
}
