using System;

namespace OdataToEntity.Db
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OeBoundFunctionAttribute : Attribute
    {
        public OeBoundFunctionAttribute()
        {
        }

        public OeBoundFunctionAttribute(String collectionFunctionName, String singleFunctionName)
        {
            CollectionFunctionName = collectionFunctionName;
            SingleFunctionName = singleFunctionName;
        }

        public String CollectionFunctionName { get; set; }
        public String SingleFunctionName { get; set; }
    }
}
