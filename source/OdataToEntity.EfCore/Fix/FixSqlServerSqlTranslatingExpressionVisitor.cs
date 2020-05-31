using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.Fix
{
    internal sealed class FixSqlServerSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
    {
        public FixSqlServerSqlTranslatingExpressionVisitor(RelationalSqlTranslatingExpressionVisitorDependencies dependencies, IModel model, QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
            : base(dependencies, model, queryableMethodTranslatingExpressionVisitor)
        {
        }

        protected override Expression VisitMember(MemberExpression memberExpression)
        {
            if (memberExpression.Expression is ConstantExpression constantExpression && Parsers.OeExpressionHelper.IsTupleType(constantExpression.Type))
            {
                var property = (PropertyInfo)memberExpression.Member;
                Object? value = property.GetValue(constantExpression.Value);
                return base.Visit(Expression.Constant(value, property.PropertyType));
            }

            return base.VisitMember(memberExpression);
        }
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Object == null
                && methodCallExpression.Method.DeclaringType == typeof(Enumerable)
                && methodCallExpression.Arguments.Count == 2
                && methodCallExpression.Arguments[0] is GroupByShaperExpression groupByShaperExpression
                && methodCallExpression.Method.Name == nameof(Enumerable.Count))
            {
                var selectorLambda = (LambdaExpression)methodCallExpression.Arguments[1];
                if (selectorLambda.Body is SelectDistinctExpression selectDistinct)
                {
                    Expression selector = ReplacingExpressionVisitor.Replace(selectDistinct.Selector.Parameters[0],
                        groupByShaperExpression.ElementSelector, selectDistinct.Selector.Body);
                    selector = base.Visit(selector);

                    SqlFragmentExpression distinctFragment = base.Dependencies.SqlExpressionFactory.Fragment("DISTINCT ");
                    RelationalTypeMapping boolTypeMapping = base.Dependencies.SqlExpressionFactory.FindMapping(typeof(bool));
                    var selectDistinctSql = new SelectDistinctSqlExpression(selector, distinctFragment, boolTypeMapping);

                    return base.Dependencies.SqlExpressionFactory.Function("COUNT", new[] { selectDistinctSql }, typeof(int));
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }
    }
}
