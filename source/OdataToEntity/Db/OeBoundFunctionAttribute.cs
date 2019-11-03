using System;

namespace OdataToEntity.Db
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OeBoundFunctionAttribute : Attribute
    {
        public OeBoundFunctionAttribute(String collectionFunctionName, String singleFunctionName)
        {
            CollectionFunctionName = collectionFunctionName;
            SingleFunctionName = singleFunctionName;
        }

        public String CollectionFunctionName { get; }
        public String SingleFunctionName { get; }
    }
}
