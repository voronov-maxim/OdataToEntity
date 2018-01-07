using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public struct OeSkipTokenTranslator
    {
        private struct OrderProperty
        {
            public readonly OrderByDirection Direction;
            public readonly PropertyInfo PropertyInfo;
            public readonly Object Value;

            public OrderProperty(PropertyInfo propertyInfo, OrderByDirection direction, Object value)
            {
                PropertyInfo = propertyInfo;
                Direction = direction;
                Value = value;
            }
        }

        private readonly OeSkipTokenParser _skipTokenParser;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSkipTokenTranslator(OeQueryNodeVisitor visitor, OeSkipTokenParser skipTokenParser)
        {
            _skipTokenParser = skipTokenParser;
            _visitor = visitor;
        }

        private static BinaryExpression CreateBinaryExpression(OeQueryNodeVisitor visitor, OrderProperty orderProperty)
        {
            MemberExpression propertyExpression = Expression.Property(visitor.Parameter, orderProperty.PropertyInfo);
            ConstantExpression constantExpression = Expression.Constant(orderProperty.Value, orderProperty.PropertyInfo.PropertyType);
            visitor.AddSkipTokenConstant(constantExpression, orderProperty.PropertyInfo.Name);

            ExpressionType binaryType;
            if (orderProperty.Value == null)
                binaryType = ExpressionType.NotEqual;
            else
                binaryType = orderProperty.Direction == OrderByDirection.Ascending ? ExpressionType.GreaterThan : ExpressionType.LessThan;

            BinaryExpression compare;
            if (propertyExpression.Type == typeof(String) && binaryType != ExpressionType.NotEqual)
            {
                Func<String, int> compareToFunc = "".CompareTo;
                MethodCallExpression compareToCall = Expression.Call(propertyExpression, compareToFunc.GetMethodInfo(), constantExpression);
                compare = Expression.MakeBinary(binaryType, compareToCall, OeConstantToVariableVisitor.ZeroStringCompareConstantExpression);
            }
            else
                compare = Expression.MakeBinary(binaryType, propertyExpression, constantExpression);
            return compare;
        }
        private static Expression CreateFilterExpression(OeQueryNodeVisitor visitor, OrderProperty[] orderProperties)
        {
            Expression filter = null;
            for (int i = 0; i < orderProperties.Length; i++)
            {
                BinaryExpression eqFilter = null;
                for (int j = 0; j < i; j++)
                {
                    MemberExpression propertyExpression = Expression.Property(visitor.Parameter, orderProperties[j].PropertyInfo);
                    ConstantExpression constantExpression = Expression.Constant(orderProperties[j].Value, propertyExpression.Type);
                    visitor.AddSkipTokenConstant(constantExpression, propertyExpression.Member.Name);

                    BinaryExpression eq = Expression.Equal(propertyExpression, constantExpression);
                    eqFilter = eqFilter == null ? eq : Expression.AndAlso(eqFilter, eq);
                }

                BinaryExpression ge = CreateBinaryExpression(visitor, orderProperties[i]);
                eqFilter = eqFilter == null ? ge : Expression.AndAlso(eqFilter, ge);

                filter = filter == null ? eqFilter : Expression.OrElse(filter, eqFilter);
            }
            return filter;
        }
        private static OrderProperty[] CreateOrderProperies(IEdmModel edmModel, OeSkipTokenParser skipTokenParser, String skipToken)
        {
            KeyValuePair<PropertyInfo, Object>[] keyValues = skipTokenParser.ParseSkipToken(skipToken);

            var orderProperties = new OrderProperty[keyValues.Length];
            for (int i = 0; i < orderProperties.Length; i++)
            {
                OrderByDirection direction = GetDirection(skipTokenParser.UniqueOrderBy, keyValues[i].Key.Name);
                orderProperties[i] = (new OrderProperty(keyValues[i].Key, direction, keyValues[i].Value));
            }
            return orderProperties;
        }
        public Expression Build(Expression source, String skipToken)
        {
            OrderProperty[] orderProperties = CreateOrderProperies(_visitor.EdmModel, _skipTokenParser, skipToken);
            Expression filter = CreateFilterExpression(_visitor, orderProperties);

            LambdaExpression lambda = Expression.Lambda(filter, _visitor.Parameter);
            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(_visitor.Parameter.Type);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        private static OrderByDirection GetDirection(OrderByClause orderByClause, String propertyName)
        {
            for (; String.Compare((orderByClause.Expression as SingleValuePropertyAccessNode).Property.Name, propertyName) != 0; orderByClause = orderByClause.ThenBy)
            {
            }
            return orderByClause.Direction;
        }
    }
}
