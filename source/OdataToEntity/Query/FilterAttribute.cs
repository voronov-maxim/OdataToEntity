using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class FilterAttribute : Attribute
    {
        private bool _disable;

        public FilterAttribute(bool disabled)
        {
            _disable = disabled;
            FilterConfigurations = new Dictionary<String, SelectExpandType>();
        }
        public FilterAttribute(params String[] properties)
        {
            FilterConfigurations = new Dictionary<String, SelectExpandType>();
            foreach (String key in properties)
                FilterConfigurations[key] = SelectExpandType.Allowed;
        }

        public bool Disabled
        {
            get
            {
                return _disable;
            }
            set
            {
                _disable = value;
                foreach (String item in new List<String>(FilterConfigurations.Keys))
                    FilterConfigurations[item] = _disable ? SelectExpandType.Disabled : SelectExpandType.Allowed;
            }
        }

        internal Dictionary<String, SelectExpandType> FilterConfigurations { get; }
    }
}
