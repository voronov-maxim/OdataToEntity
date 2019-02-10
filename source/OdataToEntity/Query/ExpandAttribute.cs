using System;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ExpandAttribute : Attribute
    {
        public ExpandAttribute(SelectExpandType expandType)
        {
            ExpandType = expandType;
        }

        public SelectExpandType ExpandType { get; }
        public int MaxDepth { get; set; }
    }
}
