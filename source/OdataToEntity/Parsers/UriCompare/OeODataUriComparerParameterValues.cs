using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers.UriCompare
{
    public struct OeODataUriComparerParameterValues
    {
        private readonly IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private readonly List<Db.OeQueryCacheDbParameterValue> _parameterValues;

        public OeODataUriComparerParameterValues(IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> constantToParameterMapper)
        {
            _constantToParameterMapper = constantToParameterMapper;
            _parameterValues = new List<Db.OeQueryCacheDbParameterValue>(_constantToParameterMapper.Count);
        }

        public void AddParameter(ConstantNode keyConstantNode, ConstantNode parameterConstanNode)
        {
            Db.OeQueryCacheDbParameterDefinition parameterDefinition = _constantToParameterMapper[keyConstantNode];
            if (parameterConstanNode.Value == null)
                _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, null));
            else
            {
                if (parameterConstanNode.Value.GetType() != parameterDefinition.ParameterType)
                {
                    ConstantExpression oldConstant = Expression.Constant(parameterConstanNode.Value, parameterConstanNode.Value.GetType());
                    ConstantExpression newConstant = OeExpressionHelper.ConstantChangeType(oldConstant, parameterDefinition.ParameterType);
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, newConstant.Value));
                }
                else
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(parameterDefinition.ParameterName, parameterConstanNode.Value));
            }
        }
        public void AddSkipParameter(long value)
        {
            foreach (KeyValuePair<ConstantNode, Db.OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Key.LiteralText == "skip")
                {
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("skip not found");
        }
        public void AddTopParameter(long value)
        {
            foreach (KeyValuePair<ConstantNode, Db.OeQueryCacheDbParameterDefinition> pair in _constantToParameterMapper)
                if (pair.Key.LiteralText == "top")
                {
                    _parameterValues.Add(new Db.OeQueryCacheDbParameterValue(pair.Value.ParameterName, (int)value));
                    return;
                }

            throw new InvalidOperationException("top not found");
        }

        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues => _parameterValues;
    }
}
