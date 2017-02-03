using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace OdataToEntity.Parsers
{
    public sealed class OeConstantToParamterVisitor : ExpressionVisitor
    {
        private readonly List<ConstantExpression> _constantNodes;
        private IReadOnlyList<MemberExpression> _propertyExpressions;

        private OeConstantToParamterVisitor()
        {
            _constantNodes = new List<ConstantExpression>();
        }

        public static Expression Translate(Expression e)
        {
            var visitor = new OeConstantToParamterVisitor();
            visitor.Visit(e);
            if (visitor._constantNodes.Count == 0)
                return e;

            NewExpression tupleNew = OeExpressionHelper.CreateTupleExpression(visitor._constantNodes);
            var tupleCtor = (Func<Object>)LambdaExpression.Lambda(tupleNew).Compile();
            Object tuple = tupleCtor();

            visitor._propertyExpressions = OeExpressionHelper.GetPropertyExpression(Expression.Constant(tuple));
            return visitor.Visit(e);
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (_propertyExpressions == null)
            {
                _constantNodes.Add(node);
                return node;
            }

            int index = _constantNodes.IndexOf(node);
            return _propertyExpressions[index];
        }
    }
}
