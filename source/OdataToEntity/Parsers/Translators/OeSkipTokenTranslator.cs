using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers.Translators
{
    public readonly struct OeSkipTokenTranslator
    {
        private readonly struct OrderProperty
        {
            public OrderProperty(IEdmProperty edmProperty, OrderByDirection direction,
                MemberExpression propertyExpression, ConstantExpression parameterExpression)
            {
                EdmProperty = edmProperty;
                Direction = direction;
                PropertyExpression = propertyExpression;
                ParameterExpression = parameterExpression;
            }

            public OrderByDirection Direction { get; }
            public IEdmProperty EdmProperty { get; }
            public ConstantExpression ParameterExpression { get; }
            public MemberExpression PropertyExpression { get; }
        }

        private readonly OeJoinBuilder _joinBuilder;
        private readonly bool _isDatabaseNullHighestValue;
        private readonly OeQueryNodeVisitor _visitor;

        public OeSkipTokenTranslator(OeQueryNodeVisitor visitor, OeJoinBuilder joinBuilder, bool isDatabaseNullHighestValue)
        {
            _visitor = visitor;
            _joinBuilder = joinBuilder;
            _isDatabaseNullHighestValue = isDatabaseNullHighestValue;
        }

        public Expression Build(Expression source, IReadOnlyList<OeSkipTokenNameValue> skipTokenNameValues, OrderByClause uniqueOrderBy)
        {
            OrderProperty[] orderProperties = CreateOrderProperies(source, skipTokenNameValues, uniqueOrderBy);
            Expression filter = CreateFilterExpression(_isDatabaseNullHighestValue, orderProperties);

            LambdaExpression lambda = Expression.Lambda(filter, _visitor.Parameter);
            MethodInfo whereMethodInfo = OeMethodInfoHelper.GetWhereMethodInfo(_visitor.Parameter.Type);
            return Expression.Call(whereMethodInfo, source, lambda);
        }
        private static BinaryExpression CreateBinaryExpression(bool isDatabaseNullHighestValue, in OrderProperty orderProperty)
        {
            Expression propertyExpression = orderProperty.PropertyExpression;
            Expression parameterExpression = orderProperty.ParameterExpression;

            Type underlyingType = Nullable.GetUnderlyingType(propertyExpression.Type) ?? propertyExpression.Type;
            if (underlyingType.IsEnum)
            {
                Type enumUnderlyingType = Enum.GetUnderlyingType(underlyingType);
                if (propertyExpression.Type != underlyingType)
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

            if (orderProperty.EdmProperty.Type.IsNullable)
            {
                if (GetDatabaseNullHighestValueDirection(orderProperty.Direction, isDatabaseNullHighestValue) == OrderByDirection.Descending)
                {
                    BinaryExpression isNull = Expression.Equal(orderProperty.PropertyExpression, OeConstantToVariableVisitor.NullConstantExpression);
                    compare = Expression.OrElse(compare, isNull);
                }
            }

            return compare;
        }
        private static Expression CreateFilterExpression(bool isDatabaseNullHighestValue, OrderProperty[] orderProperties)
        {
            if (orderProperties.Length == 0)
                throw new InvalidOperationException(nameof(orderProperties) + " is empty array");

            Expression? filter = null;
            for (int i = 0; i < orderProperties.Length; i++)
            {
                BinaryExpression? eqFilter = null;
                for (int j = 0; j < i; j++)
                {
                    Expression propertyExpression = orderProperties[j].PropertyExpression;
                    Expression parameterExpression = orderProperties[j].ParameterExpression;
                    BinaryExpression eq = Expression.Equal(propertyExpression, parameterExpression);

                    eqFilter = eqFilter == null ? eq : Expression.AndAlso(eqFilter, eq);
                }

                BinaryExpression ge;
                if (orderProperties[i].ParameterExpression == OeConstantToVariableVisitor.NullConstantExpression)
                {
                    if (GetDatabaseNullHighestValueDirection(orderProperties[i].Direction, isDatabaseNullHighestValue) == OrderByDirection.Descending)
                        continue;

                    ge = Expression.NotEqual(orderProperties[i].PropertyExpression, OeConstantToVariableVisitor.NullConstantExpression);
                }
                else
                    ge = CreateBinaryExpression(isDatabaseNullHighestValue, orderProperties[i]);
                eqFilter = eqFilter == null ? ge : Expression.AndAlso(eqFilter, ge);
                filter = filter == null ? eqFilter : Expression.OrElse(filter, eqFilter);
            }
            return filter!;
        }
        private OrderProperty[] CreateOrderProperies(Expression source, IReadOnlyList<OeSkipTokenNameValue> skipTokenNameValues, OrderByClause uniqueOrderBy)
        {
            var orderProperties = new OrderProperty[skipTokenNameValues.Count];
            for (int i = 0; i < skipTokenNameValues.Count; i++)
            {
                OrderByClause orderBy = GetOrderBy(uniqueOrderBy, skipTokenNameValues[i].Name);
                MemberExpression propertyExpression = OeOrderByTranslator.GetPropertyExpression(_joinBuilder, source, _visitor.Parameter, orderBy.Expression);

                ConstantExpression parameterExpression = skipTokenNameValues[i].Value == null ?
                    OeConstantToVariableVisitor.NullConstantExpression : _visitor.AddSkipTokenConstant(skipTokenNameValues[i], propertyExpression.Type);

                IEdmStructuralProperty edmProperty = OeSkipTokenParser.GetEdmProperty(orderBy.Expression, propertyExpression.Type);
                orderProperties[i] = new OrderProperty(edmProperty, orderBy.Direction, propertyExpression, parameterExpression);
            }
            return orderProperties;
        }
        private static OrderByDirection GetDatabaseNullHighestValueDirection(OrderByDirection direction, bool isDatabaseNullHighestValue)
        {
            if (isDatabaseNullHighestValue)
                direction = direction == OrderByDirection.Ascending ? OrderByDirection.Descending : OrderByDirection.Ascending;
            return direction;
        }
        private static OrderByClause GetOrderBy(OrderByClause orderByClause, String propertyName)
        {
            while (orderByClause != null)
            {
                IEdmProperty edmProperty = OeSkipTokenParser.GetEdmProperty(orderByClause.Expression, typeof(Decimal));
                if (String.Compare(OeSkipTokenParser.GetPropertyName(edmProperty), propertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    return orderByClause;

                orderByClause = orderByClause.ThenBy;
            }

            throw new InvalidOperationException("Property " + propertyName + " not found in OrderBy");
        }
    }
}
