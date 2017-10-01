using Microsoft.OData.Client;
using ODataClient.Default;
using System;

namespace OdataToEntity.Test
{
    partial class DbFixtureInitDb
    {
        partial void DbInit(String databaseName, bool clear)
        {
            Container container = CreateContainer();
            container.ResetDb().Execute();
            container.ResetManyColumns().Execute();

            if (!clear)
            {
                AspClient.BatchTest.Add(container);
                container.SaveChanges(SaveChangesOptions.BatchWithSingleChangeset);
            }
        }
    }

    public sealed class ManyColumnsFixtureInitDb : DbFixtureInitDb
    {
    }
}
