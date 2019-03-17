using System;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SelectAttribute : Attribute
    {
        public SelectAttribute(SelectExpandType selectType)
        {
            SelectType = selectType;
        }

        public SelectExpandType SelectType { get; }
    }
}
