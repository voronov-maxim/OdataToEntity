using GraphQL.Http;
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
            using (var context = new StarWars.StarWarsContext("test"))
                context.Database.EnsureCreated();

            String query = @"
               {
                  droid(id: ""4"") {
                    name
                  }
               }
            ";

            OeEfCoreDataAdapter<StarWars.StarWarsContext> dataAdapter = new StarWars.StarWarsDataAdapter(false, "test");
            IEdmModel edmModel = dataAdapter.BuildEdmModelFromEfCoreModel();
            var parser = new OeGraphqlParser(dataAdapter, edmModel);

            String odataUri = Uri.UnescapeDataString(parser.GetOdataUri(query).OriginalString);
            Console.WriteLine(odataUri);

            String json = new DocumentWriter(true).Write(await parser.Execute(query));
            Console.WriteLine(json);
        }
    }
}
