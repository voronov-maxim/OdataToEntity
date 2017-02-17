using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers.UriCompare
{
    public struct OeODataUriComparerParameterValues
    {
        private readonly IReadOnlyDictionary<ConstantNode, KeyValuePair<String, Type>> _constantNodeNames;
        private readonly List<KeyValuePair<String, Object>> _parameterValues;

        public OeODataUriComparerParameterValues(IReadOnlyDictionary<ConstantNode, KeyValuePair<String, Type>> constantNodeNames)
        {
            _constantNodeNames = constantNodeNames;
            _parameterValues = new List<KeyValuePair<String, Object>>(_constantNodeNames.Count);
        }

        public void AddParameter(ConstantNode keyConstantNode, ConstantNode parameterConstanNode)
        {
            KeyValuePair<String, Type> parameter =_constantNodeNames[keyConstantNode];
            if (parameterConstanNode.Value == null)
                _parameterValues.Add(new KeyValuePair<String, Object>(parameter.Key, null));
            else
            {
                if (parameterConstanNode.Value.GetType() != parameter.Value)
                {
                    ConstantExpression oldConstant = Expression.Constant(parameterConstanNode.Value, parameterConstanNode.Value.GetType());
                    ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(oldConstant, parameter.Value);
                    _parameterValues.Add(new KeyValuePair<String, Object>(parameter.Key, newConstant.Value));
                }
                else
                    _parameterValues.Add(new KeyValuePair<String, Object>(parameter.Key, parameterConstanNode.Value));
            }
        }

        public IReadOnlyList<KeyValuePair<String, Object>> ParameterValues => _parameterValues;
    }
}
