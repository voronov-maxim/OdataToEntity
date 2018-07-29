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
    public readonly struct OeOrderByTranslator
    {
        private readonly struct OrderByExpressionBuider
        {
            private readonly Func<OrderByDirection, Type, Type, MethodInfo> _getMethodInfo;
            private readonly bool _isInsertedOrderByMethod;
            private readonly OeQueryNodeVisitor _visitor;

            public OrderByExpressionBuider(OeQueryNodeVisitor visitor, Func<OrderByDirection, Type, Type, MethodInfo> getMethodInfo, bool isInsertedOrderByMethod)
            {
                _visitor = visitor;
                _getMethodInfo = getMethodInfo;
                _isInsertedOrderByMethod = isInsertedOrderByMethod;
            }

            public Expression ApplyOrderBy(Expression source, OrderByClause orderByClause, out bool isInsertedOrderByMethod)
            {
                isInsertedOrderByMethod = _isInsertedOrderByMethod;
                if (orderByClause == null)
                    return source;

                Expression keySelector = null;
                if (!_isInsertedOrderByMethod)
                {
                    var tupleProperty = new Translators.OePropertyTranslator(source);
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
                    if (propertyNode.Source is SingleNavigationNode navigationNode)
                    {
                        while (navigationNode.Source is SingleNavigationNode)
                            navigationNode = navigationNode.Source as SingleNavigationNode;
                        edmSplitType = navigationNode.NavigationProperty.DeclaringType;
                    }
                    else
                        edmSplitType = propertyNode.Property.DeclaringType;

                    isInsertedOrderByMethod = true;
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
                Type sourceTypeGeneric = _isInsertedOrderByMethod ? typeof(IOrderedEnumerable<>) : typeof(IEnumerable<>);
                var splitterVisitor = new OeExpressionSplitterVisitor(sourceTypeGeneric.MakeGenericType(sourceItemType));
                Expression beforeExpression = splitterVisitor.GetBefore(source);

                var visitor = new OeQueryNodeVisitor(_visitor.EdmModel, Expression.Parameter(sourceItemType), _visitor.Constans);
                Expression keySelector = visitor.TranslateNode(orderByClause.Expression);
                Expression orderByCall = GetOrderByExpression(beforeExpression, visitor.Parameter, orderByClause.Direction, keySelector);
                return splitterVisitor.Join(orderByCall);
            }
        }

        private readonly OeQueryNodeVisitor _visitor;

        public OeOrderByTranslator(OeQueryNodeVisitor visitor)
        {
            _visitor = visitor;
        }

        public Expression Build(Expression source, OrderByClause orderByClause)
        {
            var orderByExpressionBuider = new OrderByExpressionBuider(_visitor, GetOrderByMethodInfo, false);
            while (orderByClause != null)
            {
                source = orderByExpressionBuider.ApplyOrderBy(source, orderByClause, out bool isInsertedOrderByMethod);
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
