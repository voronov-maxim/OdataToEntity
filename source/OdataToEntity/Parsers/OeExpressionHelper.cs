using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
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

            Object value;
            if (constantExpression.Type == typeof(DateTimeOffset))
            {
                if (targetType == typeof(DateTime))
                    return Expression.Constant(((DateTimeOffset)constantExpression.Value).DateTime);
            }
            else if (constantExpression.Type == typeof(Date))
            {
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
        public static MemberInitExpression CreateTupleExpression(IReadOnlyList<Expression> expressions)
        {
            Type[] typeArguments = new Type[expressions.Count];
            for (int i = 0; i < typeArguments.Length; i++)
                typeArguments[i] = expressions[i].Type;

            Type tupleType = GetTupleType(typeArguments);
            ConstructorInfo ctorInfo = tupleType.GetTypeInfo().GetConstructor(typeArguments);
            return Expression.MemberInit(Expression.New(ctorInfo, expressions));
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
        public static Type GetTupleType(Type[] typeArguments)
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
        public static MemberExpression[] GetPropertyExpression(Expression expression)
        {
            PropertyInfo[] properties = expression.Type.GetTypeInfo().GetProperties();
            var expressions = new MemberExpression[properties.Length];
            for (int i = 0; i < properties.Length; i++)
                expressions[i] = Expression.Property(expression, properties[i]);
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
