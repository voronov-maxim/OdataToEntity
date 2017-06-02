using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace OdataToEntity.Parsers
{
    public sealed class OeParameterToVariableVisitor : ExpressionVisitor
    {
        private readonly List<ConstantExpression> _constantExpressions;
        private IReadOnlyList<Expression> _parameterExpressions;
        private IReadOnlyList<Db.OeQueryCacheDbParameterValue> _parameterValues;

        public OeParameterToVariableVisitor()
        {
            _constantExpressions = new List<ConstantExpression>();
        }

        public Expression Translate(Expression expression, IReadOnlyList<Db.OeQueryCacheDbParameterValue> parameterValues)
        {
            _parameterValues = parameterValues;
            base.Visit(expression);
            if (_constantExpressions.Count == 0)
                return expression;

            NewExpression tupleNew = OeExpressionHelper.CreateTupleExpression(_constantExpressions);
            var tupleCtor = (Func<Object>)LambdaExpression.Lambda(tupleNew).Compile();
            Object tuple = tupleCtor();

            _parameterExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Constant(tuple));
            return base.Visit(expression);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            for (int i = 0; i < _parameterValues.Count; i++)
                if (_parameterValues[i].ParameterName == node.Name)
                    if (_parameterExpressions == null)
                        _constantExpressions.Add(Expression.Constant(_parameterValues[i].ParameterValue, node.Type));
                    else
                        return _parameterExpressions[i];
            return node;
        }
    }
}
