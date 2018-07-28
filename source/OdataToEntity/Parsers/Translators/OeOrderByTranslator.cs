using Microsoft.OData.UriParser;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public static class OeOrderByTranslator
    {
        public static Expression Build(OeJoinBuilder joinBuilder, Expression source, OrderByClause orderByClause)
        {
            ParameterExpression parameterExpression = joinBuilder.Visitor.Parameter;
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                Expression keySelector = joinBuilder.GetJoinPropertyExpression(source, parameterExpression, propertyNode);
                LambdaExpression lambda = Expression.Lambda(keySelector, parameterExpression);

                MethodInfo orderByMethodInfo = GetOrderByMethodInfo(source, orderByClause.Direction, parameterExpression.Type, keySelector.Type);
                source = Expression.Call(orderByMethodInfo, source, lambda);

                orderByClause = orderByClause.ThenBy;
            }

            return source;
        }
        private static MethodInfo GetOrderByMethodInfo(Expression source, OrderByDirection direction, Type sourceType, Type keyType)
        {
            if (source.Type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                return direction == OrderByDirection.Ascending ?
                    OeMethodInfoHelper.GetThenByMethodInfo(sourceType, keyType) :
                    OeMethodInfoHelper.GetThenByDescendingMethodInfo(sourceType, keyType);
            else
                return direction == OrderByDirection.Ascending ?
                    OeMethodInfoHelper.GetOrderByMethodInfo(sourceType, keyType) :
                    OeMethodInfoHelper.GetOrderByDescendingMethodInfo(sourceType, keyType);
        }
    }
}
