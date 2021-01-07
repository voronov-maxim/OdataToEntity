using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression? _parameter;
        private readonly Expression _source;

        public ReplaceParameterVisitor(Expression source)
        {
            _source = source;
        }
        public ReplaceParameterVisitor(Expression source, ParameterExpression parameter)
        {
            _source = source;
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_parameter == null)
                return node.Type == _source.Type ? _source : base.VisitParameter(node);
            else
                return node == _parameter ? _source : base.VisitParameter(node);
        }
    }
}
