using System;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.Fix
{
    public sealed class SelectDistinctExpression : Expression
    {
        public SelectDistinctExpression(LambdaExpression selector)
        {
            Selector = selector;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;
        public LambdaExpression Selector { get; }
        public override Type Type => typeof(bool);
    }
}
