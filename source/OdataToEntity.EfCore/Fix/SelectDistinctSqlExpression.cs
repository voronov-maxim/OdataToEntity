using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.Fix
{
    public class SelectDistinctSqlExpression : SqlExpression
    {
        private readonly SqlFragmentExpression _distinctFragment;

        public SelectDistinctSqlExpression(Expression selector, SqlFragmentExpression distinctFragment, RelationalTypeMapping typeMapping)
            : base(typeof(bool), typeMapping)
        {
            Selector = selector;
            _distinctFragment = distinctFragment;
        }

        public override bool Equals(Object obj)
        {
            return obj != null && (ReferenceEquals(this, obj) || obj is SelectDistinctSqlExpression distinctCountExpression && Equals(distinctCountExpression));
        }
        private bool Equals(SelectDistinctSqlExpression distinctCountExpression)
        {
            return base.Equals(distinctCountExpression) && Selector.Equals(distinctCountExpression.Selector);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Selector);
        }
        public override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("DISTINCT ");
            expressionPrinter.Visit(Selector);
        }
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var distincFragment = (SqlFragmentExpression)visitor.Visit(_distinctFragment);
            Expression selector = visitor.Visit(Selector);
            return new SelectDistinctSqlExpression(selector, distincFragment, base.TypeMapping);
        }

        public Expression Selector { get; }
    }
}
