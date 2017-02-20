using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public class OeConstantToVariableVisitor : ExpressionVisitor
    {
        private readonly List<ConstantExpression> _constantExpressions;
        private IReadOnlyList<Expression> _parameterExpressions;

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
            NewExpression tupleNew = OeExpressionHelper.CreateTupleExpression(constantExpressions);
            var tupleCtor = (Func<Object>)LambdaExpression.Lambda(tupleNew).Compile();
            Object tuple = tupleCtor();

            return OeExpressionHelper.GetPropertyExpression(Expression.Constant(tuple));
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (_parameterExpressions == null)
            {
                Type underlyingType = null;
                if (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(node.Type) != null || node.Type.GetTypeInfo().IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(node.Type)) != null && underlyingType.GetTypeInfo().IsEnum)
                    _constantExpressions.Add(node);
                return node;
            }

            int index = _constantExpressions.IndexOf(node);
            return index == -1 ? (Expression)node : _parameterExpressions[index];
        }
    }
}
