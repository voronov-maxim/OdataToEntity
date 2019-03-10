using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OdataToEntity.GraphQL
{
    public sealed class OeEntitySetResolver : IFieldResolver<IEnumerable<Dictionary<String, Object>>>
    {
        private readonly IEdmModel _edmModel;

        public OeEntitySetResolver(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }
        public IEnumerable<Dictionary<String, Object>> Resolve(ResolveFieldContext context)
        {
            var results = new List<Dictionary<String, Object>>();

            var translator = new OeGraphqlAstToODataUri(_edmModel, context);
            ODataUri odataUri = translator.Translate(context.Document.OriginalQuery);
            IEdmModel refModel = _edmModel.GetEdmModel(odataUri.Path);
            Db.OeDataAdapter dataAdapter = refModel.GetDataAdapter(refModel.EntityContainer);
            context.UserContext = dataAdapter.CreateDataContext();

            try
            {
                var queryContext = new Parsers.OeQueryContext(refModel, odataUri, 0, false, OeMetadataLevel.Minimal);
                Db.OeAsyncEnumerator asyncEnumerator = dataAdapter.ExecuteEnumerator(context.UserContext, queryContext, CancellationToken.None);
                using (var entityAsyncEnumerator = new OeGraphqlAsyncEnumerator(asyncEnumerator, queryContext.EntryFactory, queryContext))
                {
                    while (entityAsyncEnumerator.MoveNext().GetAwaiter().GetResult())
                        results.Add(entityAsyncEnumerator.Current);
                }
            }
            finally
            {
                if (context.UserContext != null)
                    dataAdapter.CloseDataContext(context.UserContext);
            }

            return results;
        }
        Object IFieldResolver.Resolve(ResolveFieldContext context)
        {
            return Resolve(context);
        }
    }
}
