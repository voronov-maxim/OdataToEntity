using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public class OeConstantToVariableVisitor : ExpressionVisitor
    {
        public static readonly ConstantExpression NullConstantExpression = Expression.Constant(null);
        public static readonly ConstantExpression ZeroStringCompareConstantExpression = Expression.Constant(0);

        private readonly List<ConstantExpression> _constantExpressions;
        private IReadOnlyList<Expression>? _parameterExpressions;

        public OeConstantToVariableVisitor()
        {
            _constantExpressions = new List<ConstantExpression>();
        }

        public Expression Translate(Expression expression, IReadOnlyDictionary<ConstantExpression, ConstantNode> constantMappings)
        {
            base.Visit(expression);
            if (_constantExpressions.Count == 0)
                return expression;

            _parameterExpressions = TranslateParameters(_constantExpressions, constantMappings);
            return base.Visit(expression);
        }
        protected virtual IReadOnlyList<Expression> TranslateParameters(
            IReadOnlyList<ConstantExpression> constantExpressions,
            IReadOnlyDictionary<ConstantExpression, ConstantNode> constantsMapping)
        {
            Object tuple = OeExpressionHelper.GetTuple(constantExpressions);
            return OeExpressionHelper.GetPropertyExpressions(Expression.Constant(tuple));
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node == ZeroStringCompareConstantExpression || node == NullConstantExpression)
                return node;

            if (_parameterExpressions == null)
            {
                Type? underlyingType;
                if (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(node.Type) != null || node.Type.IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(node.Type)) != null && underlyingType.IsEnum)
                    if (!_constantExpressions.Contains(node) && node != NullConstantExpression)
                        _constantExpressions.Add(node);
                return node;
            }

            int index = _constantExpressions.IndexOf(node);
            return index == -1 ? node : _parameterExpressions[index];
        }
    }
}
