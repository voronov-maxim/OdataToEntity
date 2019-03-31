using System;
using System.Collections.Generic;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SelectAttribute : Attribute
    {
        private SelectExpandType _selectType;

        public SelectAttribute(SelectExpandType selectType)
        {
            _selectType = selectType;
            SelectConfigurations = new Dictionary<String, SelectExpandType>();
        }
        public SelectAttribute(params String[] properties)
        {
            SelectConfigurations = new Dictionary<String, SelectExpandType>();
            foreach (String key in properties)
                SelectConfigurations[key] = SelectExpandType.Allowed;
        }

        internal Dictionary<String, SelectExpandType> SelectConfigurations { get; }
        public SelectExpandType SelectType
        {
            get
            {
                return _selectType;
            }
            set
            {
                _selectType = value;
                foreach (String item in new List<String>(SelectConfigurations.Keys))
                    SelectConfigurations[item] = _selectType;
            }
        }
    }
}
