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
        public OeGraphqlParser(OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            DataAdapter = dataAdapter;
            EdmModel = edmModel;
            Schema = new OeSchemaBuilder(dataAdapter, edmModel).Build();
        }

        public Task<ExecutionResult> Execute(String query)
        {
            return Execute(query, null);
        }
        public async Task<ExecutionResult> Execute(String query, Inputs inputs)
        {
            Object dataContext = DataAdapter.CreateDataContext();
            try
            {
                Schema schema = Schema;
                return await new DocumentExecuter().ExecuteAsync(options =>
                {
                    options.Inputs = inputs;
                    options.Query = query;
                    options.Schema = schema;
                    options.UserContext = dataContext;
                }).ConfigureAwait(false);
            }
            finally
            {
                DataAdapter.CloseDataContext(dataContext);
            }
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

        public OeDataAdapter DataAdapter { get; }
        public IEdmModel EdmModel { get; }
        public Schema Schema { get; }
    }
}
