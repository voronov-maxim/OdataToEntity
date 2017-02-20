using System;

namespace OdataToEntity.Db
{
    public struct OeQueryCacheDbParameterDefinition
    {
        private readonly String _parameterName;
        private readonly Type _parameterType;

        public OeQueryCacheDbParameterDefinition(String parameterName, Type parameterType)
        {
            _parameterName = parameterName;
            _parameterType = parameterType;
        }

        public String ParameterName => _parameterName;
        public Type ParameterType => _parameterType;
    }
}
