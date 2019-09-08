using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.Test
{
    internal sealed class SQLiteVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression[] _newParameters;
        private readonly ReadOnlyCollection<ParameterExpression> _oldParameters;

        public SQLiteVisitor()
        {
        }
        private SQLiteVisitor(ReadOnlyCollection<ParameterExpression> oldParameters, ParameterExpression[] newParameters)
        {
            _oldParameters = oldParameters;
            _newParameters = newParameters;
        }

        private ITuple ChangeTupleConstant(ITuple tuple)
        {
            Object[] allArguments = null;
            Type type = ChangeType(tuple.GetType());
            if (type != tuple.GetType())
            {
                allArguments = new Object[tuple.Length];
                for (int i = 0; i < tuple.Length; i++)
                {
                    Object value = tuple[i];
                    if (value is DateTimeOffset dateTimeOffset)
                        allArguments[i] = dateTimeOffset.UtcDateTime;
                    else if (value is Decimal d)
                        allArguments[i] = (double)d;
                    else
                        allArguments[i] = value;
                }

                return CreateTuple(type, 0);
            }

            return tuple;

            ITuple CreateTuple(Type tupleType, int index)
            {
                Type[] typeArguments = tupleType.GetGenericArguments();
                ITuple restTuple = null;
                if (typeArguments.Length == 8)
                    restTuple = CreateTuple(typeArguments[7], index + 7);

                var arguments = new Object[typeArguments.Length];
                if (restTuple == null)
                    Array.Copy(allArguments, index, arguments, 0, typeArguments.Length);
                else
                {
                    Array.Copy(allArguments, index, arguments, 0, 7);
                    arguments[7] = restTuple;
                }

                return (ITuple)tupleType.GetConstructor(tupleType.GetGenericArguments()).Invoke(arguments);
            }
        }
        private static Type ChangeType(Type type)
        {
            if (type == typeof(DateTimeOffset))
                return typeof(DateTime);
            if (type == typeof(DateTimeOffset?))
                return typeof(DateTime?);
            if (type == typeof(Decimal))
                return typeof(double);
            if (type == typeof(Decimal?))
                return typeof(double?);

            Type[] arguments;
            if (OeExpressionHelper.IsTupleType(type))
                arguments = OeExpressionHelper.GetTupleArguments(type);
            else if (IsAnonymousType(type))
            {
                PropertyInfo[] properties = type.GetProperties();
                arguments = new Type[properties.Length];
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = properties[i].PropertyType;
            }
            else
                return type;

            bool changed = false;
            for (int i = 0; i < arguments.Length; i++)
            {
                Type argumentType = ChangeType(arguments[i]);
                changed |= argumentType != arguments[i];
                arguments[i] = argumentType;
            }

            return changed ? OeExpressionHelper.GetTupleType(arguments) : type;
        }
        private ParameterExpression GetParameter(ParameterExpression parameter)
        {
            if (_oldParameters == null)
                return parameter;

            for (int i = 0; i < _oldParameters.Count; i++)
                if (_oldParameters[i] == parameter)
                    return _newParameters[i];

            throw new InvalidOperationException("Parameter mapping not found");
        }
        private static bool IsAnonymousType(Type type)
        {
            return type.Name.StartsWith("<>", StringComparison.Ordinal) && type.Name.Contains("AnonymousType", StringComparison.Ordinal);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            Expression left = base.Visit(node.Left);
            Expression right = base.Visit(node.Right);
            if (node.Left != left || node.Right != right)
            {
                MethodInfo method = node.Method;
                if (method != null)
                {
                    Type nodeType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
                    if (nodeType != method.DeclaringType)
                        method = nodeType.GetMethod(method.Name);
                }

                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, method);
            }

            return node;
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(DateTimeOffset))
                return Expression.Constant(((DateTimeOffset)node.Value).UtcDateTime);
            if (node.Type == typeof(DateTimeOffset?))
                return Expression.Constant(node.Value == null ? (DateTime?)null : ((DateTimeOffset)node.Value).UtcDateTime, typeof(DateTime?));
            if (node.Type == typeof(Decimal))
                return Expression.Constant((double)(Decimal)node.Value);
            if (node.Type == typeof(Decimal?))
                return Expression.Constant(node.Value == null ? (double?)null : (double)(Decimal)node.Value, typeof(double?));

            if (node.Value is ITuple tuple)
                return Expression.Constant(ChangeTupleConstant(tuple));

            return node;
        }
        protected override Expression VisitExtension(Expression node)
        {
            return node;
        }
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            ParameterExpression[] parameters = null;
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = (ParameterExpression)VisitParameter(node.Parameters[i]);
                if (parameter != node.Parameters[i] || parameters != null)
                {
                    if (parameters == null)
                    {
                        parameters = new ParameterExpression[node.Parameters.Count];
                        for (int j = 0; j < i; j++)
                            parameters[i] = node.Parameters[j];
                    }

                    parameters[i] = parameter;
                }
            }

            Expression body;
            if (parameters == null)
                body = base.Visit(node.Body);
            else
                body = new SQLiteVisitor(node.Parameters, parameters).Visit(node.Body);

            if (body != node.Body || parameters != null)
            {
                Type[] arguments = node.Type.GetGenericArguments();
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = ChangeType(arguments[i]);

                Type delegateType = node.Type.GetGenericTypeDefinition().MakeGenericType(arguments);
                if (parameters == null)
                    return Expression.Lambda(delegateType, body, node.Parameters);
                else
                    return Expression.Lambda(delegateType, body, parameters);
            }

            return node;
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            Expression expression = base.Visit(node.Expression);
            PropertyInfo property = node.Member as PropertyInfo;
            if (property != null && expression.Type == node.Expression.Type)
            {
                if (property.PropertyType == typeof(DateTimeOffset))
                {
                    if (property.DeclaringType == typeof(DateTimeOffset?) && property.Name == nameof(Nullable<DateTimeOffset>.Value))
                        return Expression.MakeMemberAccess(expression, expression.Type.GetMember(node.Member.Name).Single());

                    var propertyInfo = new Infrastructure.OeShadowPropertyInfo(property.DeclaringType, typeof(DateTime), property.Name);
                    return Expression.Property(expression, propertyInfo);
                }
                if (property.PropertyType == typeof(DateTimeOffset?))
                {
                    var propertyInfo = new Infrastructure.OeShadowPropertyInfo(property.DeclaringType, typeof(DateTime?), property.Name);
                    return Expression.Property(expression, propertyInfo);
                }
            }

            if (expression != node.Expression)
            {
                if (IsAnonymousType(node.Expression.Type))
                    node = MapPropertyAnonymousTypeToTuple(expression, (PropertyInfo)node.Member);
                else
                    node = Expression.MakeMemberAccess(expression, expression.Type.GetMember(node.Member.Name).Single());
            }

            if (property != null && property.PropertyType == typeof(Decimal))
                return Expression.Convert(node, typeof(double));
            if (property != null && property.PropertyType == typeof(Decimal?))
                return Expression.Convert(node, typeof(double?));

            return node;

            MemberExpression MapPropertyAnonymousTypeToTuple(Expression tupleInstance, PropertyInfo anonymousProperty)
            {
                Type tupleType = expression.Type;
                PropertyInfo[] anonymousProperties = anonymousProperty.DeclaringType.GetProperties();
                int index = Array.IndexOf(anonymousProperties, anonymousProperty);

                MemberExpression memberAccess = null;
                while (OeExpressionHelper.IsTupleType(tupleType))
                {
                    int offset = index > 7 ? 7 : index;
                    PropertyInfo tupleProperty = tupleType.GetProperties()[offset];
                    if (memberAccess == null)
                        memberAccess = Expression.Property(tupleInstance, tupleProperty);
                    else
                        memberAccess = Expression.Property(memberAccess, tupleProperty);
                    tupleType = tupleProperty.PropertyType;

                    index -= offset;
                }
                return memberAccess;
            }
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            IReadOnlyList<Expression> arguments;
            if (node.Method.Name == nameof(Enumerable.Select))
                arguments = new Expression[] { base.Visit(node.Arguments[0]), node.Arguments[1] };
            else
                arguments = base.Visit(node.Arguments);

            Expression expression = base.Visit(node.Object);
            if (expression == node.Object && arguments == node.Arguments)
                return node;

            MethodInfo closeMethod = OeMethodInfoHelper.MakeGenericMethod(node.Method, arguments);
            return Expression.Call(node.Object, closeMethod, arguments);
        }
        protected override Expression VisitNew(NewExpression node)
        {
            ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
            if (arguments == node.Arguments)
                return node;

            return OeExpressionHelper.CreateTupleExpression(arguments);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            ParameterExpression parameter = GetParameter(node);
            if (parameter != node)
                return parameter;

            Type parameterType = ChangeType(node.Type);
            if (parameterType != node.Type)
                return Expression.Parameter(parameterType, node.Name);

            return node;
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Quote)
                return base.Visit(node.Operand);

            if (node.NodeType == ExpressionType.Convert)
            {
                if (node.Type == typeof(DateTimeOffset))
                    return Expression.Convert(base.Visit(node.Operand), typeof(DateTime));
                if (node.Type == typeof(DateTimeOffset?))
                {
                    Expression operand = base.Visit(node.Operand);
                    if (operand.Type == typeof(DateTimeOffset))
                        operand = Expression.Property(operand, nameof(DateTimeOffset.UtcDateTime));
                    return Expression.Convert(operand, typeof(DateTime?));
                }
                if (node.Type == typeof(Decimal))
                    return Expression.Convert(base.Visit(node.Operand), typeof(double));
                if (node.Type == typeof(Decimal?))
                    return Expression.Convert(base.Visit(node.Operand), typeof(double?));
            }

            return node.Update(base.Visit(node.Operand));
        }
    }
}