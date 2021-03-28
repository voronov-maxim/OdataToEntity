using GraphQL;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
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
            Schema.Initialize();
        }

        public Task<ExecutionResult> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<ExecutionResult> Execute(String query, Inputs? inputs)
        {
            Schema schema = Schema;
            return await new DocumentExecuter().ExecuteAsync(options =>
            {
                options.Inputs = inputs;
                options.Query = query;
                options.Schema = schema;
                options.ThrowOnUnhandledException = true;
            }).ConfigureAwait(false);
        }
        public Uri GetOdataUri(String query)
        {
            var context = new ResolveFieldContext()
            {
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
