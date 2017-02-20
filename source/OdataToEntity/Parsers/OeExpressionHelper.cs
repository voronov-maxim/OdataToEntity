using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Parsers
{
    public static class OeExpressionHelper
    {
        public static ConstantExpression ConstantChangeType(ConstantExpression constantExpression, Type targetType)
        {
            if (constantExpression.Value == null)
                return constantExpression;
            if (constantExpression.Type == targetType)
                return constantExpression;

            Object value;
            if (constantExpression.Type == typeof(DateTimeOffset))
            {
                if (targetType == typeof(Nullable<DateTimeOffset>))
                    return constantExpression;
                if (targetType == typeof(DateTime))
                    return Expression.Constant(((DateTimeOffset)constantExpression.Value).DateTime);
            }
            else if (constantExpression.Type == typeof(Date))
            {
                if (targetType == typeof(Nullable<Date>))
                    return constantExpression;
                if (targetType == typeof(DateTime))
                    return Expression.Constant((DateTime)((Date)constantExpression.Value));
            }
            else if (constantExpression.Type == typeof(ODataEnumValue))
            {
                var enumValue = (ODataEnumValue)constantExpression.Value;
                value = Enum.Parse(targetType, enumValue.Value);
                return Expression.Constant(value);
            }

            value = Convert.ChangeType(constantExpression.Value, targetType);
            return Expression.Constant(value);
        }
        public static NewExpression CreateTupleExpression(IReadOnlyList<Expression> expressions)
        {
            if (expressions.Count < 8 || (expressions.Count == 8 && IsTupleType(expressions[7].Type)))
            {
                Type[] typeArguments = new Type[expressions.Count];
                for (int i = 0; i < typeArguments.Length; i++)
                {
                    Type type = expressions[i].Type;
                    if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                        typeArguments[i] = typeof(IEnumerable<>).MakeGenericType(type.GetTypeInfo().GetGenericArguments());
                    else
                        typeArguments[i] = type;
                }
                Type tupleType = GetTupleType(typeArguments);
                ConstructorInfo ctorInfo = tupleType.GetTypeInfo().GetConstructor(typeArguments);
                return Expression.New(ctorInfo, expressions, tupleType.GetTypeInfo().GetProperties());
            }

            NewExpression restNew = null;
            int count = expressions.Count;
            while (count > 0)
            {
                int len = count % 7;
                if (len == 0)
                    len = 7;

                Expression[] restExpressions;
                if (restNew == null)
                    restExpressions = new Expression[len];
                else
                {
                    restExpressions = new Expression[len + 1];
                    restExpressions[len] = restNew;
                }
                for (; len > 0; len--, count--)
                    restExpressions[len - 1] = expressions[count - 1];
                restNew = CreateTupleExpression(restExpressions);
            }
            return restNew;
        }
        public static Type GetCollectionItemType(Type collectionType)
        {
            if (collectionType.GetTypeInfo().IsPrimitive)
                return null;

            if (collectionType.GetTypeInfo().IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return collectionType.GetTypeInfo().GetGenericArguments()[0];

            foreach (Type iface in collectionType.GetTypeInfo().GetInterfaces())
                if (iface.GetTypeInfo().IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetTypeInfo().GetGenericArguments()[0];
            return null;
        }
        private static Type GetTupleType(Type[] typeArguments)
        {
            Type tupleType;
            switch (typeArguments.Length)
            {
                case 1:
                    tupleType = typeof(Tuple<>);
                    break;
                case 2:
                    tupleType = typeof(Tuple<,>);
                    break;
                case 3:
                    tupleType = typeof(Tuple<,,>);
                    break;
                case 4:
                    tupleType = typeof(Tuple<,,,>);
                    break;
                case 5:
                    tupleType = typeof(Tuple<,,,,>);
                    break;
                case 6:
                    tupleType = typeof(Tuple<,,,,,>);
                    break;
                case 7:
                    tupleType = typeof(Tuple<,,,,,,>);
                    break;
                case 8:
                    tupleType = typeof(Tuple<,,,,,,,>);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Tuple out of range");
            }

            return tupleType.MakeGenericType(typeArguments);
        }
        public static IReadOnlyList<MemberExpression> GetPropertyExpression(Expression expression)
        {
            PropertyInfo[] properties = expression.Type.GetTypeInfo().GetProperties();
            var expressions = new List<MemberExpression>(properties.Length);
            for (int i = 0; i < properties.Length; i++)
            {
                MemberExpression propertyExpression = Expression.Property(expression, properties[i]);
                if (i == 7 && IsTupleType(properties[7].PropertyType))
                    expressions.AddRange(GetPropertyExpression(propertyExpression));
                else
                    expressions.Add(propertyExpression);
            }
            return expressions;
        }
        public static bool IsNull(Expression expression)
        {
            var constantExpression = expression as ConstantExpression;
            return constantExpression == null ? false : constantExpression.Value == null;
        }
        public static bool IsNullable(Expression expression)
        {
            Type type = expression.Type;
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
        public static bool IsTupleType(Type type)
        {
            if (!type.GetTypeInfo().IsGenericType)
                return false;

            Type tupleType = type.GetGenericTypeDefinition();
            return
                tupleType == typeof(Tuple<>) ||
                tupleType == typeof(Tuple<,>) ||
                tupleType == typeof(Tuple<,,>) ||
                tupleType == typeof(Tuple<,,,>) ||
                tupleType == typeof(Tuple<,,,,>) ||
                tupleType == typeof(Tuple<,,,,,>) ||
                tupleType == typeof(Tuple<,,,,,,>) ||
                tupleType == typeof(Tuple<,,,,,,,>);
        }
        public static ExpressionType ToExpressionType(BinaryOperatorKind operatorKind)
        {
            switch (operatorKind)
            {
                case BinaryOperatorKind.Or:
                    return ExpressionType.OrElse;
                case BinaryOperatorKind.And:
                    return ExpressionType.AndAlso;
                case BinaryOperatorKind.Equal:
                    return ExpressionType.Equal;
                case BinaryOperatorKind.NotEqual:
                    return ExpressionType.NotEqual;
                case BinaryOperatorKind.GreaterThan:
                    return ExpressionType.GreaterThan;
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return ExpressionType.GreaterThanOrEqual;
                case BinaryOperatorKind.LessThan:
                    return ExpressionType.LessThan;
                case BinaryOperatorKind.LessThanOrEqual:
                    return ExpressionType.LessThanOrEqual;
                case BinaryOperatorKind.Add:
                    return ExpressionType.Add;
                case BinaryOperatorKind.Subtract:
                    return ExpressionType.Subtract;
                case BinaryOperatorKind.Multiply:
                    return ExpressionType.Multiply;
                case BinaryOperatorKind.Divide:
                    return ExpressionType.Divide;
                case BinaryOperatorKind.Modulo:
                    return ExpressionType.Modulo;
                case BinaryOperatorKind.Has:
                    throw new NotImplementedException(nameof(BinaryOperatorKind.Has));
                default:
                    throw new ArgumentOutOfRangeException(nameof(operatorKind));
            }
        }
    }
}
