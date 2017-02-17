using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.UriCompare
{
    public struct OeODataUriComparerParameterValues
    {
        private readonly IReadOnlyDictionary<ConstantNode, String> _constantNodeNames;
        private readonly List<KeyValuePair<String, Object>> _parameterValues;

        public OeODataUriComparerParameterValues(IReadOnlyDictionary<ConstantNode, String> constantNodeNames)
        {
            _constantNodeNames = constantNodeNames;
            _parameterValues = new List<KeyValuePair<String, Object>>(_constantNodeNames.Count);
        }

        public void AddParameter(ConstantNode keyConstantNode, ConstantNode parameterConstanNode)
        {
            String parameterName =_constantNodeNames[keyConstantNode];
            _parameterValues.Add(new KeyValuePair<String, Object>(parameterName, parameterConstanNode.Value));
        }

        public IReadOnlyList<KeyValuePair<String, Object>> ParameterValues => _parameterValues;
    }
}
