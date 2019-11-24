using Microsoft.OData.Edm;
using System;

namespace OdataToEntity.Parsers
{
    public abstract class OeEntryFactoryFactory
    {
        public abstract OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, Type clrType, OePropertyAccessor[]? skipTokenAccessors);
    }
}
