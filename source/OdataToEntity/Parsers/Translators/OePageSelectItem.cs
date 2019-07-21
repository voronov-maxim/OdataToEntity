using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OePageSelectItem : SelectItem
    {
        public OePageSelectItem(int pageSize)
        {
            if (pageSize <= 0)
                throw new ArgumentException("Must be greater zero", nameof(pageSize));

            PageSize = pageSize;
        }

        public override void HandleWith(SelectItemHandler handler)
        {
            throw new NotImplementedException();
        }
        public override T TranslateWith<T>(SelectItemTranslator<T> translator)
        {
            throw new NotImplementedException();
        }

        public int PageSize { get; }
    }
}
