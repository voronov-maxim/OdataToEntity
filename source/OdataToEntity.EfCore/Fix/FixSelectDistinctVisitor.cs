using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.Fix
{
    public sealed class FixSelectDistinctVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node is MethodCallExpression countCall && countCall.Method.Name == nameof(Enumerable.Count) && countCall.Arguments.Count == 1 &&
                countCall.Arguments[0] is MethodCallExpression distinctCall && distinctCall.Method.Name == nameof(Enumerable.Distinct) &&
                distinctCall.Arguments[0] is MethodCallExpression selectCall && selectCall.Method.Name == nameof(Enumerable.Select))
            {
                var selectDistinct = new SelectDistinctExpression((LambdaExpression)selectCall.Arguments[1]);
                LambdaExpression lambda = Expression.Lambda(selectDistinct, selectDistinct.Selector.Parameters[0]);
                Func<IEnumerable<Object>, Func<Object, bool>, int> countFunc = Enumerable.Count;
                MethodInfo countMethod = countFunc.Method.GetGenericMethodDefinition().MakeGenericMethod(lambda.Parameters[0].Type);
                return Expression.Call(countMethod, selectCall.Arguments[0], lambda);
            }

            return base.VisitMethodCall(node);
        }
    }
}
