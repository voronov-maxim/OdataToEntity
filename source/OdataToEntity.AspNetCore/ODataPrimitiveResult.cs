using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.Threading.Tasks;

namespace OdataToEntity.AspNetCore
{
    public sealed class ODataPrimitiveResult<T> : ODataResult<T>
    {
        private readonly IEdmModel _edmModel;
        private readonly Db.OeAsyncEnumerator _items;
        private readonly ODataUri _odataUri;

        public ODataPrimitiveResult(IEdmModel edmModel, ODataUri odataUri, Db.OeAsyncEnumerator items)
        {
            _edmModel = edmModel;
            _odataUri = odataUri;
            _items = items;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await Parsers.OePostParser.WriteCollectionAsync(_edmModel, _odataUri, _items, context.HttpContext.Response.Body);
        }
    }
}
