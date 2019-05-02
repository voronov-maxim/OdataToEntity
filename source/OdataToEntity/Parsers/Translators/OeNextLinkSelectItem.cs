using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeNextLinkSelectItem : SelectItem
    {
        public static readonly OeNextLinkSelectItem Instance = new OeNextLinkSelectItem(true);

        private OeNextLinkSelectItem(bool nextLink)
        {
            NextLink = nextLink;
        }

        public override void HandleWith(SelectItemHandler handler)
        {
            throw new NotImplementedException();
        }
        public override T TranslateWith<T>(SelectItemTranslator<T> translator)
        {
            throw new NotImplementedException();
        }

        public bool NextLink { get; }
    }
}
