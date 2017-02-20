using System;

namespace OdataToEntity.Db
{
    public struct OeQueryCacheDbParameterValue
    {
        private readonly String _parameterName;
        private readonly Object _parameterValue;

        public OeQueryCacheDbParameterValue(String parameterName, Object parameterValue)
        {
            _parameterName = parameterName;
            _parameterValue = parameterValue;
        }

        public String ParameterName => _parameterName;
        public Object ParameterValue => _parameterValue;
    }
}
