using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.Translators
{
    internal sealed class OeAggregationEntryFactoryFactory : OeEntryFactoryFactory
    {
        private readonly List<OeAggregationTranslator.AggProperty> _aggProperties;

        public OeAggregationEntryFactoryFactory(List<OeAggregationTranslator.AggProperty> aggProperties)
        {
            _aggProperties = aggProperties;
        }

        public override OeEntryFactory CreateEntryFactory(IEdmEntitySet entitySet, Type clrType, OePropertyAccessor[]? skipTokenAccessors)
        {
            OePropertyAccessor[] accessors;
            if (_aggProperties.Count == 0)
                accessors = OePropertyAccessor.CreateFromType(clrType, entitySet);
            else
            {
                int groupIndex = _aggProperties.FindIndex(a => a.IsGroup);
                accessors = OePropertyAccessor.CreateFromTuple(clrType, _aggProperties, groupIndex);
            }

            return new OeEntryFactory(entitySet, accessors, skipTokenAccessors);
        }
    }
}
