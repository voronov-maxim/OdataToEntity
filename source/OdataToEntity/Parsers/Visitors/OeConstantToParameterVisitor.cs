using Microsoft.OData.UriParser;
using OdataToEntity.Cache;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeConstantToParameterVisitor : OeConstantToVariableVisitor
    {
        private readonly Dictionary<ConstantNode, OeQueryCacheDbParameterDefinition> _constantToParameterMapper;
        private OeQueryCacheDbParameterValue[]? _parameterValues;

        public OeConstantToParameterVisitor()
        {
            _constantToParameterMapper = new Dictionary<ConstantNode, OeQueryCacheDbParameterDefinition>();
        }

        protected override IReadOnlyList<Expression> TranslateParameters(
            IReadOnlyList<ConstantExpression> constantExpressions,
            IReadOnlyDictionary<ConstantExpression, ConstantNode> constantMappings)
        {
            var parameters = new ParameterExpression[constantExpressions.Count];
            _parameterValues = new OeQueryCacheDbParameterValue[constantExpressions.Count];
            for (int i = 0; i < constantExpressions.Count; i++)
            {
                ConstantExpression constantExpression = constantExpressions[i];
                String parameterName = "__p_" + i.ToString(CultureInfo.InvariantCulture);

                ConstantNode constantNode = constantMappings[constantExpression];
                _constantToParameterMapper.Add(constantNode, new OeQueryCacheDbParameterDefinition(parameterName, constantExpression.Type));

                _parameterValues[i] = new OeQueryCacheDbParameterValue(parameterName, constantExpression.Value);
                parameters[i] = Expression.Parameter(constantExpression.Type, parameterName);
            }
            return parameters;
        }

        public IReadOnlyDictionary<ConstantNode, OeQueryCacheDbParameterDefinition> ConstantToParameterMapper => _constantToParameterMapper;
        public IReadOnlyList<OeQueryCacheDbParameterValue> ParameterValues => _parameterValues ?? Array.Empty<OeQueryCacheDbParameterValue>();
    }
}
