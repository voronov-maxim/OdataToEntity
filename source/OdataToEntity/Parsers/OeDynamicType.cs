using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers
{
    public abstract class OeDynamicType
    {
        private readonly Dictionary<String, Object> _indexedProperties;

        protected OeDynamicType()
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
