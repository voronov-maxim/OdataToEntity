using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public class ODataResult<T> : IActionResult
    {
        private readonly IAsyncEnumerator<T> _entities;
        private readonly OeQueryContext _queryContext;

        protected ODataResult()
        {
        }
        public ODataResult(OeQueryContext queryContext, IAsyncEnumerator<T> entities)
        {
            _queryContext = queryContext;
            _entities = entities;
        }

        public virtual async Task ExecuteResultAsync(ActionContext context)
        {
            HttpContext httpContext = context.HttpContext;
            OeEntryFactory entryFactoryFromTuple = _queryContext.EntryFactory.GetEntryFactoryFromTuple(_queryContext.EdmModel, _queryContext.ODataUri.OrderBy);
            await Writers.OeGetWriter.SerializeAsync(_queryContext, (IAsyncEnumerator<Object>)_entities,
                httpContext.Request.ContentType, httpContext.Response.Body, entryFactoryFromTuple, null, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}