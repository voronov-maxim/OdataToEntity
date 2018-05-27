using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OdataToEntity.GraphQL
{
    public sealed class OeEntitySetResolver<T> : IFieldResolver<IEnumerable<T>>
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OeEntitySetResolver(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }
        public IEnumerable<T> Resolve(ResolveFieldContext context)
        {
            var zzz3 = context.ParentType.Fields.Single(f => f.Name == context.Document.Operations.Single().SelectionSet.Selections.OfType<Field>().Single().Name);
            var zzz = context.Schema.FindType("order");
            var zzz2 = context.Schema.Query.Fields.Single(f => f.Name == context.FieldAst.Name);

            var barcode = context.GetArgument<string>("barcode");
            var title = context.GetArgument<string>("title");
            var sellingPrice = context.GetArgument<decimal>("sellingPrice");

            var results = new List<T>();

            var odataParser = new ODataUriParser(_edmModel, new Uri("http://dummy"), new Uri("http://dummy/Orders?$expand=Customer&$select=Name"));
            odataParser.Resolver.EnableCaseInsensitive = true;
            //ODataUri odataUri = odataParser.ParseUri();

            var translator = new OeGraphQLAstToODataUri(_edmModel, context.Schema);
            ODataUri odataUri = translator.Translate(context.Document.OriginalQuery);
            var odataQuery = odataUri.BuildUri(ODataUrlKeyDelimiter.Parentheses);

            var parser = new OeGetParser(_dataAdapter, _edmModel);
            Parsers.OeQueryContext queryContext = parser.CreateQueryContext(odataUri, 0, false, OeMetadataLevel.Minimal);
            Db.OeAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteEnumerator(context.UserContext, queryContext, CancellationToken.None);
            using (var entityAsyncEnumerator = new OeEntityAsyncEnumerator<T>(queryContext.EntryFactory, asyncEnumerator, queryContext))
            {
                while (entityAsyncEnumerator.MoveNext().GetAwaiter().GetResult())
                    results.Add((T)entityAsyncEnumerator.Current);
            }
            return results;
        }

        object IFieldResolver.Resolve(ResolveFieldContext context)
        {
            return Resolve(context);
        }
    }
}
