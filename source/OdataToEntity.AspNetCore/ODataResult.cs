using Microsoft.AspNetCore.Mvc;
using OdataToEntity.Parsers;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public class ODataResult<T> : IActionResult
    {
        private readonly Db.OeAsyncEnumerator _entities;
        private readonly OeQueryContext _queryContext;

        protected ODataResult()
        {
        }
        public ODataResult(OeQueryContext queryContext, Db.OeAsyncEnumerator entities)
        {
            _queryContext = queryContext;
            _entities = entities;
        }

        public virtual async Task ExecuteResultAsync(ActionContext context)
        {
            OeEntryFactory entryFactoryFromTuple = _queryContext.EntryFactory.GetEntryFactoryFromTuple(_queryContext.EdmModel, _queryContext.ODataUri.OrderBy);
            await Writers.OeGetWriter.SerializeAsync(_queryContext, _entities, context.HttpContext.Request.ContentType, context.HttpContext.Response.Body, entryFactoryFromTuple);
        }
    }
}