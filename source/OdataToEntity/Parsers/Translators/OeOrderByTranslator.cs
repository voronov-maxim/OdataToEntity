using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public readonly struct OeOrderByTranslator
    {
        private readonly struct OrderByExpressionBuider
        {
            private readonly OeGroupJoinExpressionBuilder _groupJoinBuilder;
            private readonly OeQueryNodeVisitor _visitor;

            public OrderByExpressionBuider(OeQueryNodeVisitor visitor, OeGroupJoinExpressionBuilder groupJoinBuilder)
            {
                _visitor = visitor;
                _groupJoinBuilder = groupJoinBuilder;
            }

            public Expression ApplyOrderBy(Expression source, OrderByClause orderByClause, ref bool isInsertedOrderByMethod)
            {
                if (orderByClause == null)
                    return source;

                Expression keySelector = null;
                if (!isInsertedOrderByMethod)
                {
                    if (_visitor.Parameter.Type.IsGenericType && _visitor.Parameter.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
                    {
                        var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                        var tuplePropertyTranslator = new OePropertyTranslator(source);
                        keySelector = tuplePropertyTranslator.Build(_visitor.Parameter, propertyNode.Property);
                    }
                    else
                        keySelector = _groupJoinBuilder.GetGroupJoinPropertyExpression(source, _visitor.Parameter, orderByClause);
                }

                if (keySelector == null)
                {
                    var propertyNode = orderByClause.Expression as SingleValuePropertyAccessNode;
                    if (propertyNode == null)
                        throw new NotSupportedException("rewrite expression support only sort by property");

                    IEdmType edmSplitType;
                    if (propertyNode.Source is SingleNavigationNode navigationNode)
                    {
                        while (navigationNode.Source is SingleNavigationNode)
                            navigationNode = navigationNode.Source as SingleNavigationNode;
                        edmSplitType = navigationNode.NavigationProperty.DeclaringType;
                    }
                    else
                        edmSplitType = propertyNode.Property.DeclaringType;

                    Expression e = InsertOrderByMethod(source, orderByClause, edmSplitType, isInsertedOrderByMethod);
                    isInsertedOrderByMethod = true;
                    return e;
                }

                return GetOrderByExpression(source, _visitor.Parameter, orderByClause.Direction, keySelector);
            }
            private MethodCallExpression GetOrderByExpression(Expression source, ParameterExpression parameter, OrderByDirection direction, Expression keySelector)
            {
                LambdaExpression lambda = Expression.Lambda(keySelector, parameter);
                MethodInfo orderByMethodInfo = GetOrderByMethodInfo(source, direction, parameter.Type, keySelector.Type);
                return Expression.Call(orderByMethodInfo, source, lambda);
            }
            private Expression InsertOrderByMethod(Expression source, OrderByClause orderByClause, IEdmType edmSplitType, bool isInsertedOrderByMethod)
            {
                Type sourceItemType = _visitor.EdmModel.GetClrType(edmSplitType);
                Type sourceTypeGeneric = isInsertedOrderByMethod ? typeof(IOrderedEnumerable<>) : typeof(IEnumerable<>);
                var splitterVisitor = new OeExpressionSplitterVisitor(sourceTypeGeneric.MakeGenericType(sourceItemType));
                Expression beforeExpression = splitterVisitor.GetBefore(source);

                var visitor = new OeQueryNodeVisitor(_visitor.EdmModel, Expression.Parameter(sourceItemType), _visitor.Constans);
                Expression keySelector = visitor.TranslateNode(orderByClause.Expression);
                Expression orderByCall = GetOrderByExpression(beforeExpression, visitor.Parameter, orderByClause.Direction, keySelector);
                return splitterVisitor.Join(orderByCall);
            }
        }

        private readonly OrderByExpressionBuider _orderByBuilder;

        public OeOrderByTranslator(OeQueryNodeVisitor visitor, OeGroupJoinExpressionBuilder groupJoinBuilder)
        {
            _orderByBuilder = new OrderByExpressionBuider(visitor, groupJoinBuilder);
        }

        public Expression Build(Expression source, OrderByClause orderByClause)
        {
            bool isInsertedOrderByMethod = false;
            return Build(source, orderByClause, ref isInsertedOrderByMethod);
        }
        private Expression Build(Expression source, OrderByClause orderByClause, ref bool isInsertedOrderByMethod)
        {
            while (orderByClause != null)
            {
                source = _orderByBuilder.ApplyOrderBy(source, orderByClause, ref isInsertedOrderByMethod);
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
