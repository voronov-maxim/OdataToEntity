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
        private IReadOnlyList<Expression> _parameterExpressions;
        private readonly bool _simplifySkipTokenFilter;

        public OeConstantToVariableVisitor(bool simplifySkipTokenFilter)
        {
            _simplifySkipTokenFilter = simplifySkipTokenFilter;
            _constantExpressions = new List<ConstantExpression>();
        }

        private static bool IsSkipTokenNullFilter(Expression expression, out bool compareResult)
        {
            compareResult = false;

            var node = expression as BinaryExpression;
            if (node == null)
                return false;

            if ((node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual) &&
                node.Left is ConstantExpression constantExpression &&
                node.Right is UnaryExpression convertExpression &&
                convertExpression.Operand == OeConstantToVariableVisitor.NullConstantExpression)
            {
                compareResult = node.NodeType == ExpressionType.Equal ? constantExpression.Value == null : constantExpression.Value != null;
                return true;
            }

            return false;
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
            var tupleCtor = (Func<Object>)Expression.Lambda(tupleNew).Compile();
            Object tuple = tupleCtor();

            return OeExpressionHelper.GetPropertyExpressions(Expression.Constant(tuple));
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (_simplifySkipTokenFilter)
            {
                if (IsSkipTokenNullFilter(node.Left, out bool compareResult))
                {
                    if (node.NodeType == ExpressionType.OrElse)
                        return node.Right;

                    if (node.NodeType == ExpressionType.AndAlso)
                        return compareResult ? node.Right : null;
                }
                if (IsSkipTokenNullFilter(node.Right, out compareResult))
                {
                    if (node.NodeType == ExpressionType.OrElse)
                        return node.Left;

                    if (node.NodeType == ExpressionType.AndAlso)
                        return compareResult ? node.Left : null;
                }

                if (node.NodeType == ExpressionType.OrElse || node.NodeType == ExpressionType.AndAlso)
                {
                    Expression left = base.Visit(node.Left);
                    Expression right = base.Visit(node.Right);
                    if (left == null)
                        return right;
                    if (right == null)
                        return left;

                    return Expression.MakeBinary(node.NodeType, left, right);
                }
            }

            return base.VisitBinary(node);
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node == ZeroStringCompareConstantExpression || node == NullConstantExpression)
                return node;

            if (_parameterExpressions == null)
            {
                Type underlyingType = null;
                if (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(node.Type) != null || node.Type.IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(node.Type)) != null && underlyingType.IsEnum)
                    if (!_constantExpressions.Contains(node))
                        _constantExpressions.Add(node);
                return node;
            }

            int index = _constantExpressions.IndexOf(node);
            return index == -1 ? (Expression)node : _parameterExpressions[index];
        }
    }
}
