using Microsoft.OData.Client;
using ODataClient.Default;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    partial class DbFixtureInitDb
    {
        partial void DbInit(String databaseName, bool clear)
        {
            DbInitAsync(clear).GetAwaiter().GetResult();
        }

        private async static Task DbInitAsync(bool clear)
        {
            Container container = CreateContainer(0);
            await container.ResetDb().ExecuteAsync();
            await container.ResetManyColumns().ExecuteAsync();

            if (!clear)
            {
                AspClient.BatchTest.Add(container);
                await container.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);
            }
        }
    }

    public sealed class ManyColumnsFixtureInitDb : DbFixtureInitDb
    {
    }
}
