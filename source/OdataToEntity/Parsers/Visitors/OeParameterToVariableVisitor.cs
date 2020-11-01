using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeParameterToVariableVisitor : ExpressionVisitor
    {
        private readonly SortedDictionary<String, ConstantExpression> _constantExpressions;
        private Dictionary<String, MemberExpression>? _propertyExpressions;
        private IReadOnlyList<Cache.OeQueryCacheDbParameterValue> _parameterValues;

        public OeParameterToVariableVisitor()
        {
            _constantExpressions = new SortedDictionary<String, ConstantExpression>(StringComparer.Ordinal);
            _parameterValues = Array.Empty<Cache.OeQueryCacheDbParameterValue>();
        }

        public Expression Translate(Expression expression, IReadOnlyList<Cache.OeQueryCacheDbParameterValue> parameterValues)
        {
            _parameterValues = parameterValues;
            base.Visit(expression);
            if (_constantExpressions.Count == 0)
                return expression;

            var constantExpressions = new ConstantExpression[_constantExpressions.Count];
            int i = 0;
            foreach (KeyValuePair<String, ConstantExpression> constantExpression in _constantExpressions)
                constantExpressions[i++] = constantExpression.Value;
            Object tuple = OeExpressionHelper.GetTuple(constantExpressions);

            IReadOnlyList<MemberExpression> tupleProperties = OeExpressionHelper.GetPropertyExpressions(Expression.Constant(tuple));
            _propertyExpressions = new Dictionary<String, MemberExpression>(tupleProperties.Count);
            var constants = _constantExpressions.GetEnumerator();
            for (i = 0; i < tupleProperties.Count; i++)
            {
                constants.MoveNext();
                _propertyExpressions.Add(constants.Current.Key, tupleProperties[i]);
            }
            constants.Dispose();

            return base.Visit(expression);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_propertyExpressions == null)
            {
                for (int i = 0; i < _parameterValues.Count; i++)
                    if (_parameterValues[i].ParameterName == node.Name)
                    {
                        if (!_constantExpressions.ContainsKey(node.Name))
                            _constantExpressions.Add(node.Name, Expression.Constant(_parameterValues[i].ParameterValue, node.Type));
                    }
            }
            else
            {
                if (node.Name != null && _propertyExpressions.TryGetValue(node.Name, out MemberExpression? propertyExpression))
                    return propertyExpression;
            }

            return node;
        }
    }
}
