using System;

namespace OdataToEntity.Query
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class CountAttribute : Attribute
    {
        public bool Disabled
        {
            get;
            set;
        }
    }
}
