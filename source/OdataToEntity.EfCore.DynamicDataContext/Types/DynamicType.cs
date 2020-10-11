using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public abstract class DynamicType : OeIndexerProperty
    {
        private readonly Dictionary<String, Object> _indexedProperties;

        protected DynamicType()
        {
            _indexedProperties = new Dictionary<String, Object>();
        }

        public Object this[String name]
        {
            get => _indexedProperties[name];
            set => _indexedProperties[name] = value;
        }
    }
}
