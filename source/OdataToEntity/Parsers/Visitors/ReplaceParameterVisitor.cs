using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _source;

        public ReplaceParameterVisitor(ParameterExpression parameter, Expression source)
        {
            _parameter = parameter;
            _source = source;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _parameter ? _source : base.VisitParameter(node);
        }
    }
}
