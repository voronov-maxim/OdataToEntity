using GraphQL;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.Threading.Tasks;

namespace OdataToEntity.GraphQL
{
    public readonly struct OeGraphqlParser
    {
        public OeGraphqlParser(IEdmModel edmModel)
        {
            EdmModel = edmModel;
            Schema = new OeSchemaBuilder(edmModel).Build();
        }

        public Task<ExecutionResult> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<ExecutionResult> Execute(String query, Inputs inputs)
        {
            Schema schema = Schema;
            return await new DocumentExecuter().ExecuteAsync(options =>
            {
                options.Inputs = inputs;
                options.Query = query;
                options.Schema = schema;
            }).ConfigureAwait(false);
        }
        public Uri GetOdataUri(String query)
        {
            return GetOdataUri(query, null);
        }
        public Uri GetOdataUri(String query, Inputs inputs)
        {
            var context = new ResolveFieldContext()
            {
                Arguments = inputs,
                Schema = Schema
            };
            var translator = new OeGraphqlAstToODataUri(EdmModel, context);
            ODataUri odataUri = translator.Translate(query);
            return odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);
        }

        public IEdmModel EdmModel { get; }
        public Schema Schema { get; }
    }
}
