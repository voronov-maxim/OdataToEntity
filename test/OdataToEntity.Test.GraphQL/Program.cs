using GraphQL;
using GraphQL.Http;
using GraphQL.Types;
using GraphQL.Utilities;
using GraphQLParser;
using GraphQLParser.AST;
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
            var dataAdapter = new OrderDbDataAdapter(false, false, "test");
            IEdmModel edmModel = dataAdapter.BuildEdmModel();
            await InitializeAsync(dataAdapter, edmModel);

            var schemaBuilder = new OeSchemaBuilder(dataAdapter, edmModel, new ModelBuilder.OeEdmModelMetadataProvider());

            var schema = schemaBuilder.Build();
            var printer = new SchemaPrinter(schema);
            var jsonSchema = printer.Print();

            Object dataContext = dataAdapter.CreateDataContext();

            var result = await new DocumentExecuter().ExecuteAsync(options =>
            {
                options.UserContext = dataContext;
                options.Schema = schema;
                //options.Query = "query { orders { name customer (address: \"RU\") { name } } }";
                options.Query = "query { orders (id: 1) { items (orderId: 1) { product } } }";
            }).ConfigureAwait(false);

            dataAdapter.CloseDataContext(dataContext);

            var json = new DocumentWriter(indent: true).Write(result);
        }
        private static async Task InitializeAsync(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            var parser = new OeParser(new Uri("http://dummy/"), dataAdapter, edmModel);
            String fileName = Directory.EnumerateFiles(".", "Add.batch", SearchOption.AllDirectories).First();
            byte[] bytes = File.ReadAllBytes(fileName);
            var responseStream = new MemoryStream();

            await parser.ExecuteBatchAsync(new MemoryStream(bytes), responseStream, CancellationToken.None).ConfigureAwait(false);
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
