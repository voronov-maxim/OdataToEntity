using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

namespace OdataToEntity.Parsers.Translators
{
    public sealed class OeDisableSelectItem : SelectItem
    {
        public OeDisableSelectItem(IEdmStructuralProperty structuralProperty)
        {
            StructuralProperty = structuralProperty;
        }

        public override void HandleWith(SelectItemHandler handler)
        {
            throw new NotImplementedException();
        }
        public override T TranslateWith<T>(SelectItemTranslator<T> translator)
        {
            throw new NotImplementedException();
        }

        public IEdmStructuralProperty StructuralProperty { get; }
    }
}
