using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeConstantToParameterVisitor : OeConstantToVariableVisitor
    {
        private readonly Dictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private Db.OeQueryCacheDbParameterValue[] _parameterValues;

        public OeConstantToParameterVisitor()
        {
            _constantToParameterMapper = new Dictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition>();
        }

        protected override IReadOnlyList<Expression> TranslateParameters(
            IReadOnlyList<ConstantExpression> constantExpressions,
            IReadOnlyDictionary<ConstantExpression, ConstantNode> constantMappings)
        {
            var parameters = new ParameterExpression[constantExpressions.Count];
            _parameterValues = new Db.OeQueryCacheDbParameterValue[constantExpressions.Count];
            for (int i = 0; i < constantExpressions.Count; i++)
            {
                ConstantExpression constantExpression = constantExpressions[i];
                String parameterName = "__p_" + i.ToString();

                ConstantNode constantNode = constantMappings[constantExpression];
                _constantToParameterMapper.Add(constantNode, new Db.OeQueryCacheDbParameterDefinition(parameterName, constantExpression.Type));

                _parameterValues[i] = new Db.OeQueryCacheDbParameterValue(parameterName, constantExpression.Value);
                parameters[i] = Expression.Parameter(constantExpression.Type, parameterName);
            }
            return parameters;
        }

        public IReadOnlyDictionary<ConstantNode, Db.OeQueryCacheDbParameterDefinition> ConstantToParameterMapper => _constantToParameterMapper;
        public IReadOnlyList<Db.OeQueryCacheDbParameterValue> ParameterValues => _parameterValues ?? Array.Empty<Db.OeQueryCacheDbParameterValue>();
    }
}
