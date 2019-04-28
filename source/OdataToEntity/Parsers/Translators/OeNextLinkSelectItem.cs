using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeNextLinkSelectItem : SelectItem
    {
        public OeNextLinkSelectItem(bool navigationNextLink)
        {
            NavigationNextLink = navigationNextLink;
        }

        public override void HandleWith(SelectItemHandler handler)
        {
            throw new NotImplementedException();
        }
        public override T TranslateWith<T>(SelectItemTranslator<T> translator)
        {
            throw new NotImplementedException();
        }

        public bool NavigationNextLink { get; }
    }
}
