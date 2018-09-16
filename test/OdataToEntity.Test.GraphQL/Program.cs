using GraphQL;
using GraphQL.Http;
using GraphQL.Types;
using GraphQL.Utilities;
using GraphQLParser;
using GraphQLParser.AST;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.GraphQL;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test.GraphQL
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var context = new StarWars.StarWarsContext("test");
            context.Database.EnsureCreated();

            var droids = context.Droid.Include(h => h.CharacterToCharacter).ToArray();
            var humans = context.Human.Include(h => h.CharacterToCharacter).ToArray();
            var heroes = context.Hero.Include(h => h.CharacterToCharacter).ThenInclude(cc => cc.FriendTo).ToArray();

            var dataAdapter = new StarWars.StarWarsDataAdapter(false, "test");
            IEdmModel edmModel = dataAdapter.BuildEdmModelFromEfCoreModel();

            var schemaBuilder = new OeSchemaBuilder(dataAdapter, edmModel, new ModelBuilder.OeEdmModelMetadataProvider());

            var schema = schemaBuilder.Build();
            var printer = new SchemaPrinter(schema);
            var jsonSchema = printer.Print();

            Object dataContext = dataAdapter.CreateDataContext();

            var query = @"
                query HeroNameAndFriendsQuery {
                  hero {
                    id
                    name
                    friends {
                      name
                    }
                  }
                }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(options =>
            {
                options.UserContext = dataContext;
                options.Schema = schema;
                options.Query = query;
            }).ConfigureAwait(false);

            dataAdapter.CloseDataContext(dataContext);

            var json = new DocumentWriter(indent: true).Write(result);
        }
        private static void Test()
        {
            var parser = new Parser(new Lexer());
            string schemaJson = File.ReadAllText(@"E:\graphql.schema");
            var source = new Source(schemaJson);
            GraphQLDocument document = parser.Parse(source);

            var querySource = new Source("query { orders (id: 1) { customer (address: \"rus\") { name } } }");
            var zzz = parser.Parse(querySource);

        }
    }
}
