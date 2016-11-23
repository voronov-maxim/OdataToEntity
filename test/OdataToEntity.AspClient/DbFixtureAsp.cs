using ODataClient.Default;
using OdataToEntity.Test.Model;
using System;
using System.Net.Http;

namespace OdataToEntity.Test
{
    partial class DbFixture
    {
        partial void DbInit(String databaseName, bool clear)
        {
            var client = new HttpClient() { BaseAddress = CreateContainer().BaseUri };
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "db/reset")).GetAwaiter().GetResult();
            if (!clear)
            {
                using (var context = OrderContext.Create(databaseName))
                    context.InitDb();

                client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "db/init")).GetAwaiter().GetResult();
            }
        }
    }
}
