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
            public ConstantExpression ParameterExpression;
            public MemberExpression PropertyExpression;
            public readonly SingleValuePropertyAccessNode PropertyNode;
            public readonly Object Value;

            public OrderProperty(SingleValuePropertyAccessNode propertyNode, OrderByDirection direction, Object value)
            {
                PropertyNode = propertyNode;
                Direction = direction;
                Value = value;

                ParameterExpression = null;
                PropertyExpression = null;
            }
        }

        private readonly OeSkipTokenParser _skipTokenParser;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSkipTokenTranslator(OeQueryNodeVisitor visitor, OeSkipTokenParser skipTokenParser)
        {
            _skipTokenParser = skipTokenParser;
            _visitor = visitor;
        }

        public Expression Build(Expression source, String skipToken)
        {
            OrderProperty[] orderProperties = CreateOrderProperies(_skipTokenParser, skipToken);
            Expression filter = CreateFilterExpression(source, _visitor, _skipTokenParser.IsDatabaseNullHighestValue, orderProperties);

            LambdaExpression lambda = Expression.Lambda(filter, _visitor.Parameter);
            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(_visitor.Parameter.Type);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        private static BinaryExpression CreateBinaryExpression(OeQueryNodeVisitor visitor, bool isDatabaseNullHighestValue, ref OrderProperty orderProperty)
        {
            Expression propertyExpression = orderProperty.PropertyExpression;
            Expression parameterExpression = orderProperty.ParameterExpression;

            Type underlyingType = Nullable.GetUnderlyingType(propertyExpression.Type);
            bool isNullableType = underlyingType != null;
            if (!isNullableType)
                underlyingType = propertyExpression.Type;

            if (underlyingType.IsEnum)
            {
                Type enumUnderlyingType = Enum.GetUnderlyingType(underlyingType);
                if (isNullableType)
                    enumUnderlyingType = typeof(Nullable<>).MakeGenericType(enumUnderlyingType);

                propertyExpression = Expression.Convert(propertyExpression, enumUnderlyingType);
                parameterExpression = Expression.Convert(parameterExpression, enumUnderlyingType);
            }

            ExpressionType binaryType = orderProperty.Direction == OrderByDirection.Ascending ? ExpressionType.GreaterThan : ExpressionType.LessThan;
            BinaryExpression compare;
            if (propertyExpression.Type == typeof(String))
            {
                Func<String, String, int> compareToFunc = String.Compare;
                MethodCallExpression compareToCall = Expression.Call(null, compareToFunc.GetMethodInfo(), propertyExpression, parameterExpression);
                compare = Expression.MakeBinary(binaryType, compareToCall, OeConstantToVariableVisitor.ZeroStringCompareConstantExpression);
            }
            else
                compare = Expression.MakeBinary(binaryType, propertyExpression, parameterExpression);

            propertyExpression = orderProperty.PropertyExpression;
            parameterExpression = orderProperty.ParameterExpression;
            if (orderProperty.PropertyNode.TypeReference.IsNullable)
            {
                BinaryExpression isNull;
                UnaryExpression typedNull = Expression.Convert(OeConstantToVariableVisitor.NullConstantExpression, parameterExpression.Type);

                OrderByDirection direction = orderProperty.Direction;
                if (isDatabaseNullHighestValue)
                    direction = orderProperty.Direction == OrderByDirection.Ascending ? OrderByDirection.Descending : OrderByDirection.Ascending;

                if (direction == OrderByDirection.Ascending)
                {
                    BinaryExpression isNullParameter = Expression.Equal(parameterExpression, typedNull);
                    BinaryExpression isNotNullProperty = Expression.NotEqual(propertyExpression, OeConstantToVariableVisitor.NullConstantExpression);
                    isNull = Expression.AndAlso(isNullParameter, isNotNullProperty);
                }
                else
                {
                    BinaryExpression isNotNullParameter = Expression.NotEqual(parameterExpression, typedNull);
                    BinaryExpression isNullProperty = Expression.Equal(propertyExpression, typedNull);
                    isNull = Expression.AndAlso(isNotNullParameter, isNullProperty);
                }
                compare = Expression.OrElse(compare, isNull);
            }

            return compare;
        }
        private static Expression CreateFilterExpression(Expression source, OeQueryNodeVisitor visitor, bool isDatabaseNullHighestValue, OrderProperty[] orderProperties)
        {
            Expression filter = null;
            for (int i = 0; i < orderProperties.Length; i++)
            {
                BinaryExpression eqFilter = null;
                for (int j = 0; j < i; j++)
                {
                    Expression propertyExpression = orderProperties[j].PropertyExpression;
                    Expression parameterExpression = orderProperties[j].ParameterExpression;
                    BinaryExpression eq = Expression.Equal(propertyExpression, parameterExpression);

                    if (OeExpressionHelper.IsNullable(propertyExpression))
                    {
                        UnaryExpression typedNull = Expression.Convert(OeConstantToVariableVisitor.NullConstantExpression, parameterExpression.Type);
                        BinaryExpression isNull = Expression.Equal(parameterExpression, typedNull);
                        eq = Expression.OrElse(eq, isNull);
                    }

                    eqFilter = eqFilter == null ? eq : Expression.AndAlso(eqFilter, eq);
                }

                InitializeOrderPropertyExpression(source, visitor, ref orderProperties[i]);

                BinaryExpression ge = CreateBinaryExpression(visitor, isDatabaseNullHighestValue, ref orderProperties[i]);
                eqFilter = eqFilter == null ? ge : Expression.AndAlso(eqFilter, ge);
                filter = filter == null ? eqFilter : Expression.OrElse(filter, eqFilter);
            }
            return filter;
        }
        private static OrderProperty[] CreateOrderProperies(OeSkipTokenParser skipTokenParser, String skipToken)
        {
            var orderProperties = new List<OrderProperty>();
            foreach (KeyValuePair<String, Object> keyValue in skipTokenParser.ParseSkipToken(skipToken))
            {
                OrderByClause orderBy = GetOrderBy(skipTokenParser.UniqueOrderBy, keyValue.Key);
                var propertyNode = (SingleValuePropertyAccessNode)orderBy.Expression;
                orderProperties.Add(new OrderProperty(propertyNode, orderBy.Direction, keyValue.Value));
            }
            return orderProperties.ToArray();
        }
        private static OrderByClause GetOrderBy(OrderByClause orderByClause, String propertyName)
        {
            while (orderByClause != null)
            {
                IEdmProperty edmProperty = ((SingleValuePropertyAccessNode)orderByClause.Expression).Property;
                if (String.Compare(OeSkipTokenParser.GetPropertyName(edmProperty), propertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    return orderByClause;

                orderByClause = orderByClause.ThenBy;
            }

            throw new InvalidOperationException("Property " + propertyName + " not found in OrderBy");
        }
        private static void InitializeOrderPropertyExpression(Expression source, OeQueryNodeVisitor visitor, ref OrderProperty orderProperty)
        {
            orderProperty.PropertyExpression = (MemberExpression)visitor.TranslateNode(orderProperty.PropertyNode);
            if (orderProperty.PropertyExpression == null)
            {
                var tupleProperty = new OePropertyTranslator(source);
                orderProperty.PropertyExpression = tupleProperty.Build(visitor.Parameter, orderProperty.PropertyNode.Property);
            }

            orderProperty.ParameterExpression = Expression.Constant(orderProperty.Value, orderProperty.PropertyExpression.Type);
            visitor.AddSkipTokenConstant(orderProperty.ParameterExpression, OeSkipTokenParser.GetPropertyName(orderProperty.PropertyExpression));
        }
    }
}
