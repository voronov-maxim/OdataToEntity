namespace OdataToEntity.Test.GraphQL
{
    public sealed class StarWarsFixture : DbFixture
    {
        public StarWarsFixture() : base(new StarWars.StarWarsDataAdapter(false, "test"))
        {
            using (var context = new StarWars.StarWarsContext("test"))
                context.Database.EnsureCreated();
        }
    }
}
