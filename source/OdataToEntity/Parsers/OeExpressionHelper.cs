using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.Parsers
{
    public static class OeExpressionHelper
    {
        private static readonly Type[] ArithmethicTypes = new Type[] { typeof(sbyte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(Decimal) };

        public static ConstantExpression ConstantChangeType(ConstantExpression constantExpression, Type targetType)
        {
            if (constantExpression.Value == null || constantExpression.Type == targetType)
                return constantExpression;

            Object value;
            if (constantExpression.Type == typeof(DateTimeOffset) || constantExpression.Type == typeof(DateTimeOffset?))
            {
                if (targetType == typeof(DateTimeOffset?))
                    return constantExpression;
                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                    return Expression.Constant(((DateTimeOffset)constantExpression.Value).UtcDateTime, targetType);
            }
            else if (constantExpression.Type == typeof(Date))
            {
                if (targetType == typeof(Date?))
                    return constantExpression;
                if (targetType == typeof(DateTime) || targetType == typeof(Date?))
                    return Expression.Constant((DateTime)(Date)constantExpression.Value, targetType);
            }
            else if (constantExpression.Type == typeof(ODataEnumValue))
            {
                var enumValue = (ODataEnumValue)constantExpression.Value;
                Type? underlyingType = Nullable.GetUnderlyingType(targetType);
                value = underlyingType == null ? Enum.Parse(targetType, enumValue.Value) : Enum.Parse(underlyingType, enumValue.Value);
                return Expression.Constant(value, targetType);
            }

            if (Nullable.GetUnderlyingType(targetType) == constantExpression.Type)
                return Expression.Constant(constantExpression.Value, targetType);

            value = Convert.ChangeType(constantExpression.Value, targetType, CultureInfo.InvariantCulture);
            return Expression.Constant(value);
        }
        public static BinaryOperatorNode CreateFilterExpression(SingleValueNode singleValueNode, IEnumerable<KeyValuePair<IEdmStructuralProperty, Object?>> keys)
        {
            BinaryOperatorNode? compositeNode = null;
            foreach (KeyValuePair<IEdmStructuralProperty, Object?> keyValue in keys)
            {
                var left = new SingleValuePropertyAccessNode(singleValueNode, keyValue.Key);
                var right = new ConstantNode(keyValue.Value, ODataUriUtils.ConvertToUriLiteral(keyValue.Value, ODataVersion.V4));
                var node = new BinaryOperatorNode(BinaryOperatorKind.Equal, left, right);

                if (compositeNode == null)
                    compositeNode = node;
                else
                    compositeNode = new BinaryOperatorNode(BinaryOperatorKind.And, compositeNode, node);
            }
            return compositeNode ?? throw new InvalidOperationException("Parameter keys is empty collection");
        }
        public static NewExpression CreateTupleExpression(IReadOnlyList<Expression> expressions)
        {
            if (expressions.Count < 8 || (expressions.Count == 8 && IsTupleType(expressions[7].Type)))
            {
                Type[] typeArguments = new Type[expressions.Count];
                for (int i = 0; i < typeArguments.Length; i++)
                {
                    Type type = expressions[i].Type;
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                        typeArguments[i] = typeof(IEnumerable<>).MakeGenericType(type.GetGenericArguments());
                    else
                        typeArguments[i] = type;
                }
                Type tupleType = GetTupleType(typeArguments);
                ConstructorInfo ctorInfo = tupleType.GetConstructor(typeArguments)!;
                return Expression.New(ctorInfo, expressions, tupleType.GetProperties());
            }

            NewExpression? restNew = null;
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
            return restNew!;
        }
        private static Type GetArithmethicPrecedenceType(Type leftType, Type rightType)
        {
            if (leftType == rightType)
                return leftType;

            leftType = GetUnsignedToSignedType(leftType);
            rightType = GetUnsignedToSignedType(rightType);

            int leftIndex = Array.IndexOf(ArithmethicTypes, leftType);
            if (leftIndex == -1)
                throw new InvalidOperationException("cannot convert not numeric type");

            int rightIndex = Array.IndexOf(ArithmethicTypes, rightType);
            if (rightIndex == -1)
                throw new InvalidOperationException("cannot convert not numeric type");

            return ArithmethicTypes[Math.Max(leftIndex, rightIndex)];
        }
        public static Type GetCollectionItemType(Type collectionType)
        {
            return GetCollectionItemTypeOrNull(collectionType) ?? throw new InvalidOperationException("Type " + collectionType.Name + " is not collection type");
        }
        public static Type? GetCollectionItemTypeOrNull(Type type)
        {
            if (type.IsPrimitive || type == typeof(String))
                return null;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            foreach (Type iface in type.GetInterfaces())
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    Type itemType = iface.GetGenericArguments()[0];
                    if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                        return null;

                    return itemType;
                }

            return null;
        }
        public static IReadOnlyList<MemberExpression> GetPropertyExpressions(Expression instance)
        {
            PropertyInfo[] properties = instance.Type.GetProperties();
            var expressions = new List<MemberExpression>(properties.Length);
            for (int i = 0; i < properties.Length; i++)
            {
                MemberExpression propertyExpression = Expression.Property(instance, properties[i]);
                if (i == 7 && IsTupleType(properties[7].PropertyType))
                    expressions.AddRange(GetPropertyExpressions(propertyExpression));
                else
                    expressions.Add(propertyExpression);
            }
            return expressions;
        }
        public static Type[] GetTupleArguments(Type tupleType)
        {
            Type[] arguments = tupleType.GetGenericArguments();
            if (arguments.Length == 8 && IsTupleType(arguments[7]))
            {
                Type[] restArguments = GetTupleArguments(arguments[7]);
                var allArguments = new Type[7 + restArguments.Length];
                Array.Copy(arguments, 0, allArguments, 0, 7);
                Array.Copy(restArguments, 0, allArguments, 7, restArguments.Length);
                return allArguments;
            }
            return arguments;
        }
        public static ITuple GetTuple(IReadOnlyList<ConstantExpression> constantExpressions)
        {
            int i = 0;
            return GetTuple(constantExpressions, ref i);
        }
        private static ITuple GetTuple(IReadOnlyList<ConstantExpression> constantExpressions, ref int i)
        {
            int length = constantExpressions.Count - i;
            if (length > 8)
                length = 8;

            var arguments = new Object?[length];
            var typeArguments = new Type[length];
            for (int j = 0; i < constantExpressions.Count; i++, j++)
                if (j == 7)
                {
                    Object rest = GetTuple(constantExpressions, ref i);
                    arguments[j] = rest;
                    typeArguments[j] = rest.GetType();
                }
                else
                {
                    arguments[j] = constantExpressions[i].Value;
                    typeArguments[j] = constantExpressions[i].Type;
                }
            Type tupleType = OeExpressionHelper.GetTupleType(typeArguments);
            return (ITuple)Activator.CreateInstance(tupleType, arguments)!;
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
                default:
                    {
                        tupleType = typeof(Tuple<,,,,,,,>);
                        if (typeArguments.Length > 8 || !IsTupleType(typeArguments[7]))
                        {
                            var restTypeArguments = new Type[typeArguments.Length - 7];
                            Array.Copy(typeArguments, 7, restTypeArguments, 0, restTypeArguments.Length);
                            Type restType = GetTupleType(restTypeArguments);
                            Array.Resize(ref typeArguments, 8);
                            typeArguments[7] = restType;
                        }
                        break;
                    }
            }

            return tupleType.MakeGenericType(typeArguments);
        }
        public static Type GetTypeConversion(Type leftType, Type rightType)
        {
            Type? leftUnderlyingType = Nullable.GetUnderlyingType(leftType);
            Type? rightUnderlyingType = Nullable.GetUnderlyingType(rightType);

            if (leftUnderlyingType == null && rightUnderlyingType == null)
                return GetArithmethicPrecedenceType(leftType, rightType);

            Type precedenceType;
            if (leftUnderlyingType == null)
            {
                if (leftType == rightUnderlyingType)
                    return typeof(Nullable<>).MakeGenericType(leftType);

                precedenceType = GetArithmethicPrecedenceType(leftType, rightUnderlyingType!);
                return typeof(Nullable<>).MakeGenericType(precedenceType);
            }

            if (rightUnderlyingType == null)
            {
                if (rightType == leftUnderlyingType)
                    return typeof(Nullable<>).MakeGenericType(rightType);

                precedenceType = GetArithmethicPrecedenceType(rightType, leftUnderlyingType);
                return typeof(Nullable<>).MakeGenericType(precedenceType);
            }

            precedenceType = GetArithmethicPrecedenceType(leftUnderlyingType, rightUnderlyingType);
            return typeof(Nullable<>).MakeGenericType(precedenceType);
        }
        private static Type GetUnsignedToSignedType(Type type)
        {
            if (type == typeof(byte))
                return typeof(short);
            if (type == typeof(ushort))
                return typeof(int);
            if (type == typeof(uint))
                return typeof(long);
            if (type == typeof(ulong))
                return typeof(Decimal);
            return type;
        }
        public static bool IsEntityType(Type entityType)
        {
            if (entityType.IsValueType)
                return false;
            if (entityType == typeof(String))
                return false;

            return true;
        }
        public static bool IsEntityType(IEdmModel edmModel, Type entityType)
        {
            if (!IsEntityType(entityType))
                return false;

            if (edmModel.EntityContainer != null)
                foreach (IEdmEntitySet entitySet in edmModel.EntityContainer.EntitySets())
                    if (edmModel.GetClrType(entitySet) == entityType)
                        return true;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                if (refModel is EdmModel)
                    return IsEntityType(refModel, entityType);

            return false;
        }
        public static bool IsNull(Expression expression)
        {
            return expression is ConstantExpression constantExpression && constantExpression.Value == null;
        }
        public static bool IsPrimitiveType(Type clrType)
        {
            if (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(clrType) != null || clrType.IsEnum)
                return true;

            Type? underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null && (ModelBuilder.PrimitiveTypeHelper.GetPrimitiveType(underlyingType) != null || underlyingType.IsEnum))
                return true;

            return false;
        }
        public static bool IsTupleType(Type type)
        {
            return typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type);
        }
        public static MemberExpression ReplaceParameter(MemberExpression propertyExpression, Expression newParameter)
        {
            var properties = new Stack<PropertyInfo>();
            MemberExpression? memberExpression = propertyExpression;
            do
            {
                properties.Push((PropertyInfo)memberExpression.Member);
                memberExpression = memberExpression.Expression as MemberExpression;
            }
            while (memberExpression != null);

            Expression expression = Expression.Convert(newParameter, properties.Peek().DeclaringType!);
            while (properties.Count > 0)
                expression = Expression.Property(expression, properties.Pop());

            return (MemberExpression)expression;
        }
        public static ExpressionType ToExpressionType(BinaryOperatorKind operatorKind)
        {
            return operatorKind switch
            {
                BinaryOperatorKind.Or => ExpressionType.OrElse,
                BinaryOperatorKind.And => ExpressionType.AndAlso,
                BinaryOperatorKind.Equal => ExpressionType.Equal,
                BinaryOperatorKind.NotEqual => ExpressionType.NotEqual,
                BinaryOperatorKind.GreaterThan => ExpressionType.GreaterThan,
                BinaryOperatorKind.GreaterThanOrEqual => ExpressionType.GreaterThanOrEqual,
                BinaryOperatorKind.LessThan => ExpressionType.LessThan,
                BinaryOperatorKind.LessThanOrEqual => ExpressionType.LessThanOrEqual,
                BinaryOperatorKind.Add => ExpressionType.Add,
                BinaryOperatorKind.Subtract => ExpressionType.Subtract,
                BinaryOperatorKind.Multiply => ExpressionType.Multiply,
                BinaryOperatorKind.Divide => ExpressionType.Divide,
                BinaryOperatorKind.Modulo => ExpressionType.Modulo,
                BinaryOperatorKind.Has => throw new NotImplementedException(nameof(BinaryOperatorKind.Has)),
                _ => throw new ArgumentOutOfRangeException(nameof(operatorKind)),
            };
        }
        public static bool TryGetConstantValue(Expression expression, out Object? value)
        {
            value = null;

            MemberExpression? propertyExpression = expression as MemberExpression;
            if (propertyExpression == null && expression is UnaryExpression convertExpression)
                propertyExpression = convertExpression.Operand as MemberExpression;

            if (propertyExpression == null)
                return false;

            if (propertyExpression.Expression is ConstantExpression constantExpression)
            {
                value = ((PropertyInfo)propertyExpression.Member).GetValue(constantExpression.Value);
                return true;
            }

            return false;
        }
    }
}