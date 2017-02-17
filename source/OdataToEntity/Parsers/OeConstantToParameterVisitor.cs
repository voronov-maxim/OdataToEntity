using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeConstantToParameterVisitor : OeConstantToVariableVisitor
    {
        private readonly Dictionary<ConstantNode, String> _constantNodeNames;
        private KeyValuePair<String, Object>[] _parameterValues;

        public OeConstantToParameterVisitor()
        {
            _constantNodeNames = new Dictionary<ConstantNode, String>();
        }

        protected override IReadOnlyList<Expression> TranslateParameters(
            IReadOnlyList<ConstantExpression> constantExpressions,
            IReadOnlyDictionary<ConstantExpression, ConstantNode> constantMappings)
        {
            var parameters = new ParameterExpression[constantExpressions.Count];
            _parameterValues = new KeyValuePair<String, Object>[constantExpressions.Count];
            for (int i = 0; i < constantExpressions.Count; i++)
            {
                ConstantExpression constantExpression = constantExpressions[i];
                String parameterName = "__p_" + i.ToString();

                ConstantNode constantNode = constantMappings[constantExpression];
                _constantNodeNames.Add(constantNode, parameterName);

                _parameterValues[i] = new KeyValuePair<String, Object>(parameterName, constantExpression.Value);
                parameters[i] = Expression.Parameter(constantExpression.Type, parameterName);
            }
            return parameters;
        }

        public IReadOnlyDictionary<ConstantNode, String> ConstantNodeNames => _constantNodeNames;
        public IReadOnlyList<KeyValuePair<String, Object>> ParameterValues => _parameterValues ?? Array.Empty<KeyValuePair<String, Object>>();
    }
}
