using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.GraphQL;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.Test.GraphQL
{
    class Program
    {
        static async Task Main(String[] args)
        {
            var tests = new StarWarsTests(new StarWarsFixture());
            await tests.can_query_for_friends_of_humans();
        }

        private static async Task Test()
        {
            using (var starWarsContext = new StarWars.StarWarsContext("StarWars"))
                starWarsContext.Database.EnsureCreated();

            using (var order2Context = new Model.Order2Context(StarWars.StarWarsContext.Create<Model.Order2Context>("Order2")))
                order2Context.Database.EnsureCreated();

            String starWarsQuery = @"
               {
                  droid(id: ""4"") {
                    name
                  }
               }
            ";
            String order2Query = @"
               {
                  orders2 {
                    name
                    customer {
                        name
                    }
                  }
               }
            ";

            var order2DataAdapter2 = new Model.Order2DataAdapter(false, "Order2");
            EdmModel refModel = order2DataAdapter2.BuildEdmModelFromEfCoreModel();

            var starWarsDataAdapter1 = new StarWars.StarWarsDataAdapter(false, "StarWars");
            EdmModel rootModel = starWarsDataAdapter1.BuildEdmModelFromEfCoreModel(refModel);

            var parser = new OeGraphqlParser(rootModel);

            String starWarsOdataUri = Uri.UnescapeDataString(parser.GetOdataUri(starWarsQuery).OriginalString);
            Console.WriteLine(starWarsOdataUri);

            String starWarsJson = await (await parser.Execute(starWarsQuery)).ToStringAsync();
            Console.WriteLine(starWarsJson);

            String order2OdataUri = Uri.UnescapeDataString(parser.GetOdataUri(order2Query).OriginalString);
            Console.WriteLine(order2OdataUri);

            String order2Json = await (await parser.Execute(order2Query)).ToStringAsync();
            Console.WriteLine(order2Json);
        }
    }
}
