using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.GraphQL
{
    public sealed class OeEntitySetResolver : IFieldResolver<Task<IEnumerable<Dictionary<String, Object?>>>>
    {
        private readonly IEdmModel _edmModel;

        public OeEntitySetResolver(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }
        public async Task<IEnumerable<Dictionary<String, Object?>>> Resolve(IResolveFieldContext context)
        {
            var results = new List<Dictionary<String, Object?>>();

            var translator = new OeGraphqlAstToODataUri(_edmModel, context);
            ODataUri odataUri = translator.Translate(context.Document.OriginalQuery);
            IEdmModel refModel = _edmModel.GetEdmModel(odataUri.Path);
            Db.OeDataAdapter dataAdapter = refModel.GetDataAdapter(refModel.EntityContainer);
            Object dataContext = dataAdapter.CreateDataContext();
            OeGraphqlAsyncEnumerator? entityAsyncEnumerator = null;
            try
            {
                var queryContext = new Parsers.OeQueryContext(refModel, odataUri);
                IAsyncEnumerator<Object> asyncEnumerator = dataAdapter.Execute(dataContext, queryContext).GetAsyncEnumerator();

                if (queryContext.EntryFactory == null)
                    throw new InvalidOperationException("queryContext.EntryFactory must be not null");

                entityAsyncEnumerator = new OeGraphqlAsyncEnumerator(asyncEnumerator, queryContext.EntryFactory, CancellationToken.None);
                while (await entityAsyncEnumerator.MoveNextAsync())
                    results.Add(entityAsyncEnumerator.Current);
            }
            finally
            {
                if (dataContext != null)
                    dataAdapter.CloseDataContext(dataContext);
                if (entityAsyncEnumerator != null)
                    await entityAsyncEnumerator.DisposeAsync().ConfigureAwait(false);
            }

            return results;
        }
        Task<IEnumerable<Dictionary<String, Object?>>> IFieldResolver<Task<IEnumerable<Dictionary<String, Object?>>>>.Resolve(IResolveFieldContext context)
        {
            throw new NotImplementedException();
        }
        Object IFieldResolver.Resolve(IResolveFieldContext context)
        {
            return Resolve(context);
        }
    }
}
