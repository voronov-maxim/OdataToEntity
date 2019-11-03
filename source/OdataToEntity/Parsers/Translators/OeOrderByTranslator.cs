using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public static class OeOrderByTranslator
    {
        public static Expression Build(OeJoinBuilder joinBuilder, Expression source, ParameterExpression parameterExpression, OrderByClause orderByClause)
        {
            while (orderByClause != null)
            {
                MemberExpression propertyExpression = GetPropertyExpression(joinBuilder, source, parameterExpression, orderByClause.Expression);
                LambdaExpression lambda = Expression.Lambda(propertyExpression, parameterExpression);

                MethodInfo orderByMethodInfo = GetOrderByMethodInfo(source, orderByClause.Direction, parameterExpression.Type, propertyExpression.Type);
                source = Expression.Call(orderByMethodInfo, source, lambda);

                orderByClause = orderByClause.ThenBy;
            }

            return source;
        }
        public static Expression BuildNested(OeJoinBuilder joinBuilder, Expression source, ParameterExpression parameterExpression, OrderByClause orderByClause,
            IReadOnlyList<IEdmNavigationProperty> joinPath)
        {
            while (orderByClause != null)
            {
                var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                Expression? keySelector = joinBuilder.GetJoinPropertyExpression(source, joinBuilder.Visitor.Parameter, joinPath, propertyNode.Property);
                if (keySelector == null)
                    throw new InvalidOperationException("Sorting EdmProperty " + propertyNode.Property.Name + " not found in source");

                LambdaExpression lambda = Expression.Lambda(keySelector, parameterExpression);
                MethodInfo orderByMethodInfo = GetOrderByMethodInfo(source, orderByClause.Direction, parameterExpression.Type, keySelector.Type);
                source = Expression.Call(orderByMethodInfo, source, lambda);

                orderByClause = orderByClause.ThenBy;
            }

            return source;
        }
        public static MemberExpression GetPropertyExpression(OeJoinBuilder joinBuilder, Expression source, Expression parameterExpression, SingleValueNode sortProperty)
        {
            if (sortProperty is SingleValuePropertyAccessNode propertyNode)
                return joinBuilder.GetJoinPropertyExpression(source, parameterExpression, propertyNode);

            if (sortProperty is SingleValueOpenPropertyAccessNode openPropertyNode)
            {
                var propertyExpression = (MemberExpression)joinBuilder.Visitor.TranslateNode(openPropertyNode);
                var replaceParameterVisitor = new ReplaceParameterVisitor(joinBuilder.Visitor.Parameter, parameterExpression);
                return (MemberExpression)replaceParameterVisitor.Visit(propertyExpression);
            }

            throw new InvalidOperationException("Unknown type order by expression " + sortProperty.GetType().Name);
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
