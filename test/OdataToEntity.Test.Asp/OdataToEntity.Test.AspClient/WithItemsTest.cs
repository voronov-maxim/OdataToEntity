using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    partial class SelectTest
    {
        [Fact]
        public async Task WithItems()
        {
            String request = $"Orders/WithItems(itemIds=[1,2,3])";

            IList<Model.OrderItem> fromOe = null;
            Uri uri = DbFixtureInitDb.ContainerFactory().BaseUri;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", OeRequestHeaders.JsonDefault.ContentType);
                using (HttpResponseMessage httpResponseMessage = await client.GetAsync(uri.LocalPath + "/" + request))
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        using (Stream content = await httpResponseMessage.Content.ReadAsStreamAsync())
                        {
                            var responseReader = new ResponseReader(Fixture.EdmModel);
                            fromOe = responseReader.Read<Model.OrderItem>(content).ToList();
                        }
                    }
            }

            Assert.Equal(new[] { 1, 2, 3 }, fromOe == null ? Enumerable.Empty<int>() : fromOe.Select(i => i.Id).OrderBy(id => id));
        }
    }
}
