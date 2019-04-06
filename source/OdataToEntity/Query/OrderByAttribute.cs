using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class OrderByAttribute : Attribute
    {
        private bool _disable;

        public OrderByAttribute(bool disabled)
        {
            _disable = disabled;
            OrderByConfigurations = new Dictionary<String, SelectExpandType>();
        }
        public OrderByAttribute(params String[] properties)
        {
            OrderByConfigurations = new Dictionary<String, SelectExpandType>();
            foreach (String key in properties)
                OrderByConfigurations[key] = SelectExpandType.Allowed;
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
                foreach (String item in new List<String>(OrderByConfigurations.Keys))
                    OrderByConfigurations[item] = _disable ? SelectExpandType.Disabled : SelectExpandType.Allowed;
            }
        }

        internal Dictionary<String, SelectExpandType> OrderByConfigurations { get; }
    }
}
