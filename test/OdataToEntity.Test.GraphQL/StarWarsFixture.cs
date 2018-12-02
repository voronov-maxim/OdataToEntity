namespace OdataToEntity.Test.GraphQL
{
    public sealed class StarWarsFixture : DbFixture
    {
        public StarWarsFixture() :
            base(new Model.Order2DataAdapter(false, "Order2"), new StarWars.StarWarsDataAdapter(false, "StarWars"))
        {
            using (var order2Context = new Model.Order2Context(StarWars.StarWarsContext.Create<Model.Order2Context>("Order2")))
                order2Context.Database.EnsureCreated();

            using (var starWarsContext = new StarWars.StarWarsContext("StarWars"))
                starWarsContext.Database.EnsureCreated();
        }
    }
}
