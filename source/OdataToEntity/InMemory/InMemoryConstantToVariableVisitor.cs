using Microsoft.OData.UriParser;
using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace OdataToEntity.InMemory
{
    public sealed class InMemoryConstantToVariableVisitor : OeConstantToParameterVisitor
    {
        private readonly List<ConstantExpression> _constantExpressions;
        private Object?[]? _parameters;

        public InMemoryConstantToVariableVisitor()
        {
            _constantExpressions = new List<ConstantExpression>();
        }

        public override Expression Translate(Expression expression, IReadOnlyDictionary<ConstantExpression, ConstantNode> constantMappings)
        {
            base.Visit(expression);
            if (_constantExpressions.Count == 0)
            {
                _parameters = Array.Empty<Object>();
                return expression;
            }

            base.TranslateParameters(_constantExpressions, constantMappings);

            _parameters = new Object[_constantExpressions.Count];
            return base.Visit(expression);
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node == ZeroStringCompareConstantExpression || node == NullConstantExpression)
                return node;

            if (_parameters == null)
            {
                Type? underlyingType;
                if (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(node.Type) != null || node.Type.IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(node.Type)) != null && underlyingType.IsEnum)
                    if (!_constantExpressions.Contains(node) && node != NullConstantExpression)
                        _constantExpressions.Add(node);
                return node;
            }

            int index = _constantExpressions.IndexOf(node);
            if (index == -1)
                return node;

            BinaryExpression parameter = Expression.ArrayIndex(Expression.Constant(_parameters), Expression.Constant(index));
            return Expression.Convert(parameter, node.Type);
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (_parameters != null && node.Operand is ConstantExpression constant)
            {
                int index = _constantExpressions.IndexOf(constant);
                if (index == -1)
                    return node;

                Expression parameter = Expression.ArrayIndex(Expression.Constant(_parameters), Expression.Constant(index));
                if (node.Type == typeof(int?) && (Nullable.GetUnderlyingType(constant.Type) ?? constant.Type).IsEnum)
                    parameter = Expression.Convert(parameter, constant.Type);
                return Expression.Convert(parameter, node.Type);
            }

            return base.VisitUnary(node);
        }

        public Object?[] Parameters => _parameters ?? throw new InvalidOperationException("Invoke Translate");
    }
}
