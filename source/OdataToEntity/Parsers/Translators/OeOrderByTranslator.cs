using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public struct OeOrderByTranslator
    {
        private struct OrderByExpressionBuider
        {
            private readonly Func<OrderByDirection, Type, Type, MethodInfo> _getMethodInfo;
            private bool _isInsertedOrderByMethod;
            private readonly OeQueryNodeVisitor _visitor;

            public OrderByExpressionBuider(OeQueryNodeVisitor visitor, Func<OrderByDirection, Type, Type, MethodInfo> getMethodInfo, bool isInsertedOrderByMethod)
            {
                _visitor = visitor;
                _getMethodInfo = getMethodInfo;
                _isInsertedOrderByMethod = isInsertedOrderByMethod;
            }

            public Expression ApplyOrderBy(Expression source, OrderByClause orderByClause)
            {
                if (orderByClause == null)
                    return source;

                Expression keySelector = null;
                if (!IsInsertedOrderByMethod)
                {
                    var tupleProperty = new OePropertyTranslator(source);
                    _visitor.TuplePropertyByEdmProperty = tupleProperty.Build;

                    if (OeExpressionHelper.IsTupleType(_visitor.Parameter.Type))
                    {
                        var propertyNode = (SingleValuePropertyAccessNode)orderByClause.Expression;
                        keySelector = tupleProperty.Build(_visitor.Parameter, propertyNode.Property);
                    }

                    if (keySelector == null)
                        keySelector = _visitor.TranslateNode(orderByClause.Expression);
                }

                if (keySelector == null)
                {
                    var propertyNode = orderByClause.Expression as SingleValuePropertyAccessNode;
                    if (propertyNode == null)
                        throw new NotSupportedException("rewrite expression support only sort by property");

                    IEdmType edmSplitType;
                    var navigationNode = propertyNode.Source as SingleNavigationNode;
                    if (navigationNode == null)
                        edmSplitType = propertyNode.Property.DeclaringType;
                    else
                    {
                        while (navigationNode.Source is SingleNavigationNode)
                            navigationNode = navigationNode.Source as SingleNavigationNode;
                        edmSplitType = navigationNode.NavigationProperty.DeclaringType;
                    }
                    return InsertOrderByMethod(source, orderByClause, edmSplitType);
                }

                return GetOrderByExpression(source, _visitor.Parameter, orderByClause.Direction, keySelector);
            }
            private MethodCallExpression GetOrderByExpression(Expression source, ParameterExpression parameter, OrderByDirection direction, Expression keySelector)
            {
                LambdaExpression lambda = Expression.Lambda(keySelector, parameter);
                MethodInfo orderByMethodInfo = _getMethodInfo(direction, parameter.Type, keySelector.Type);
                return Expression.Call(orderByMethodInfo, source, lambda);
            }
            private Expression InsertOrderByMethod(Expression source, OrderByClause orderByClause, IEdmType edmSplitType)
            {
                Type sourceItemType = _visitor.EdmModel.GetClrType(edmSplitType);
                Type sourceTypeGeneric = IsInsertedOrderByMethod ? typeof(IOrderedEnumerable<>) : typeof(IEnumerable<>);
                var splitterVisitor = new OeExpressionSplitterVisitor(sourceTypeGeneric.MakeGenericType(sourceItemType));
                Expression beforeExpression = splitterVisitor.GetBefore(source);

                var visitor = new OeQueryNodeVisitor(_visitor.EdmModel, Expression.Parameter(sourceItemType), _visitor.Constans);
                Expression keySelector = visitor.TranslateNode(orderByClause.Expression);
                Expression orderByCall = GetOrderByExpression(beforeExpression, visitor.Parameter, orderByClause.Direction, keySelector);
                _isInsertedOrderByMethod = true;
                return splitterVisitor.Join(orderByCall);
            }

            public bool IsInsertedOrderByMethod => _isInsertedOrderByMethod;
        }

        private readonly OeQueryNodeVisitor _visitor;

        public OeOrderByTranslator(OeQueryNodeVisitor visitor)
        {
            _visitor = visitor;
        }

        public Expression Build(Expression source, OrderByClause orderByClause)
        {
            bool isInsertedOrderByMethod = false;
            var orderByExpressionBuider = new OrderByExpressionBuider(_visitor, GetOrderByMethodInfo, false);
            while (orderByClause != null)
            {
                source = orderByExpressionBuider.ApplyOrderBy(source, orderByClause);
                isInsertedOrderByMethod |= orderByExpressionBuider.IsInsertedOrderByMethod;

                orderByExpressionBuider = new OrderByExpressionBuider(_visitor, GetThenByMethodInfo, isInsertedOrderByMethod);
                orderByClause = orderByClause.ThenBy;
            }

            return source;
        }
        private static MethodInfo GetOrderByMethodInfo(OrderByDirection direction, Type sourceType, Type keyType)
        {
            return direction == OrderByDirection.Ascending ?
                OeMethodInfoHelper.GetOrderByMethodInfo(sourceType, keyType) :
                OeMethodInfoHelper.GetOrderByDescendingMethodInfo(sourceType, keyType);
        }
        private static MethodInfo GetThenByMethodInfo(OrderByDirection direction, Type sourceType, Type keyType)
        {
            return direction == OrderByDirection.Ascending ?
                OeMethodInfoHelper.GetThenByMethodInfo(sourceType, keyType) :
                OeMethodInfoHelper.GetThenByDescendingMethodInfo(sourceType, keyType);
        }
    }
}
