using GraphQL.Language.AST;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OdataToEntity.GraphQL
{
    public sealed class OeEntitySetResolver : IFieldResolver<IEnumerable<Dictionary<String, Object>>>
    {
        private readonly Db.OeDataAdapter _dataAdapter;
        private readonly IEdmModel _edmModel;

        public OeEntitySetResolver(Db.OeDataAdapter dataAdapter, IEdmModel edmModel)
        {
            _dataAdapter = dataAdapter;
            _edmModel = edmModel;
        }
        public IEnumerable<Dictionary<String, Object>> Resolve(ResolveFieldContext context)
        {
            var results = new List<Dictionary<String, Object>>();

            var translator = new OeGraphQLAstToODataUri(_edmModel, context.Schema);
            ODataUri odataUri = translator.Translate(context.Document.OriginalQuery);

            var parser = new OeGetParser(_dataAdapter, _edmModel);
            Parsers.OeQueryContext queryContext = parser.CreateQueryContext(odataUri, 0, false, OeMetadataLevel.Minimal);
            Db.OeAsyncEnumerator asyncEnumerator = _dataAdapter.ExecuteEnumerator(context.UserContext, queryContext, CancellationToken.None);
            using (var entityAsyncEnumerator = new OeGraphqlAsyncEnumerator(asyncEnumerator, queryContext.EntryFactory, queryContext))
            {
                while (entityAsyncEnumerator.MoveNext().GetAwaiter().GetResult())
                    results.Add(entityAsyncEnumerator.Current);
            }

            return results;
        }

        object IFieldResolver.Resolve(ResolveFieldContext context)
        {
            return Resolve(context);
        }
    }
}
