using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class ODataPrimitiveResult<T> : ODataResult<T>
    {
        private readonly IEdmModel _edmModel;
        private readonly IAsyncEnumerator<T> _items;
        private readonly ODataUri _odataUri;

        public ODataPrimitiveResult(IEdmModel edmModel, ODataUri odataUri, IAsyncEnumerator<T> items)
        {
            _edmModel = edmModel;
            _odataUri = odataUri;
            _items = items;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await Parsers.OePostParser.WriteCollectionAsync(_edmModel, _odataUri,
                (IAsyncEnumerator<Object>)_items, context.HttpContext.Response.Body, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}
