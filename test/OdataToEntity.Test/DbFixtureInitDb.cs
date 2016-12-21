namespace OdataToEntity.Test
{
    public sealed class DbFixtureInitDb : DbFixture
    {
        public DbFixtureInitDb()
        {
            base.ExecuteBatchAsync("Add").GetAwaiter().GetResult();
        }
    }
}
