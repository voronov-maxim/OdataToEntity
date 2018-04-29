using System;

namespace OdataToEntity.Db
{
    public readonly struct OeQueryCacheDbParameterDefinition
    {
        public OeQueryCacheDbParameterDefinition(String parameterName, Type parameterType)
        {
            ParameterName = parameterName;
            ParameterType = parameterType;
        }

        public String ParameterName { get; }
        public Type ParameterType { get; }
    }
}
