using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class ODataPrimitiveResult<T> : ODataResult<T>
    {
        private sealed class AsyncEnumeratorAdapter : Db.OeAsyncEnumerator
        {
            private readonly IAsyncEnumerator<T> _items;

            public AsyncEnumeratorAdapter(IAsyncEnumerator<T> items)
                : base(CancellationToken.None)
            {
                _items = items;
            }

            public override void Dispose()
            {
                _items.Dispose();
            }
            public override Task<bool> MoveNextAsync()
            {
                return _items.MoveNext();
            }

            public override Object Current => _items.Current;
        }

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
            var adapter = new AsyncEnumeratorAdapter(_items);
            await Parsers.OePostParser.WriteCollectionAsync(_edmModel, _odataUri, adapter, context.HttpContext.Response.Body);
        }
    }
}
