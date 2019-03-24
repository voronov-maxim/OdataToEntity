using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class FilterAttribute : Attribute
    {
        private bool _disable;

        private FilterAttribute(bool disabled)
        {
            _disable = disabled;
        }
        public FilterAttribute(params String[] properties)
        {
            FilterConfigurations = new Dictionary<String, bool>();
            foreach (String key in properties)
                FilterConfigurations[key] = true;
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
                if (FilterConfigurations != null)
                    foreach (String item in FilterConfigurations.Keys)
                        FilterConfigurations[item] = !_disable;
            }
        }

        public Dictionary<String, bool> FilterConfigurations { get; }
    }
}
