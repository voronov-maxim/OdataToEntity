using System;

namespace OdataToEntity.Test
{
    public class DbFunctionAttribute : Attribute
    {
        public DbFunctionAttribute()
        {
        }
        public DbFunctionAttribute(String functionName, String schema = null)
        {
            FunctionName = functionName;
            Schema = schema;
        }

        public virtual String FunctionName { get; set; }
        public virtual String Schema { get; set; }
    }
}
