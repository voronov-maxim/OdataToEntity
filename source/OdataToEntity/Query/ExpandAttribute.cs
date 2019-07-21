using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ExpandAttribute : Attribute
    {
        private SelectExpandType _expandType;

        public ExpandAttribute(SelectExpandType expandType)
        {
            _expandType = expandType;
            ExpandConfigurations = new Dictionary<String, SelectExpandType>();
        }
        public ExpandAttribute(params String[] properties)
        {
            ExpandConfigurations = new Dictionary<String, SelectExpandType>();
            foreach (String key in properties)
                ExpandConfigurations[key] = SelectExpandType.Allowed;
        }

        internal Dictionary<String, SelectExpandType> ExpandConfigurations { get; }
        public SelectExpandType ExpandType
        {
            get
            {
                return _expandType;
            }
            set
            {
                _expandType = value;
                foreach (String item in new List<String>(ExpandConfigurations.Keys))
                    ExpandConfigurations[item] = _expandType;
            }
        }
        public int MaxDepth { get; set; }
    }
}
