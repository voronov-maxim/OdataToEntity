using OdataToEntity.Parsers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

            if (OeExpressionHelper.IsTupleType(type))
            {
                Type[] arguments = OeExpressionHelper.GetTupleArguments(type);
                bool changed = false;
                for (int i = 0; i < arguments.Length; i++)
                {
                    Type argumentType = ChangeType(arguments[i]);
                    changed |= argumentType != arguments[i];
                    arguments[i] = argumentType;
                }

                if (changed)
                    return OeExpressionHelper.GetTupleType(arguments);
            }

            return type;
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
        protected override Expression VisitBinary(BinaryExpression node)
        {
            Expression left = base.Visit(node.Left);
            Expression right = base.Visit(node.Right);
            if (node.Left != left || node.Right != right)
                return Expression.MakeBinary(node.NodeType, left, right);

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
            if (property != null)
            {
                if (property.PropertyType == typeof(DateTimeOffset))
                {
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
                node = Expression.MakeMemberAccess(expression, expression.Type.GetMember(node.Member.Name).Single());

            if (property != null && property.PropertyType == typeof(Decimal))
                return Expression.Convert(node, typeof(double));
            if (property != null && property.PropertyType == typeof(Decimal?))
                return Expression.Convert(node, typeof(double?));

            return node;
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression expression = base.Visit(node.Object);
            ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
            if (expression == node.Object && arguments == node.Arguments)
                return node;

            if (node.Method.DeclaringType == typeof(Enumerable) && node.Method.Name == nameof(Enumerable.Sum))
            {
                Type returnType = ((LambdaExpression)arguments[1]).ReturnType;
                MethodInfo openMethod = OeMethodInfoHelper.GetAggMethodInfo(node.Method.Name, returnType);
                MethodInfo closeMethod = openMethod.MakeGenericMethod(node.Method.GetGenericArguments());
                return Expression.Call(node.Object, closeMethod, arguments);
            }

            if (node.Method.DeclaringType == typeof(Queryable))
            {
                Type[] genericArguments = node.Method.GetGenericArguments();
                if (node.Method.Name == nameof(Queryable.Select))
                {
                    if (arguments[1] is UnaryExpression quote)
                        genericArguments[1] = ((LambdaExpression)quote.Operand).ReturnType;
                    else
                        genericArguments[1] = ((LambdaExpression)arguments[1]).ReturnType;

                    MethodInfo closeMethod = node.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArguments);
                    return Expression.Call(node.Object, closeMethod, arguments);
                }

                if (node.Method.Name == nameof(Queryable.OrderBy))
                {
                    genericArguments[0] = genericArguments[0].GetGenericTypeDefinition().MakeGenericType(arguments[0].Type.GetGenericArguments()[0].GetGenericArguments());
                    MethodInfo closeMethod = node.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArguments);
                    return Expression.Call(node.Object, closeMethod, arguments);
                }
            }

            return node;
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
                    return Expression.Convert(base.Visit(node.Operand), typeof(DateTime?));
                if (node.Type == typeof(Decimal))
                    return Expression.Convert(base.Visit(node.Operand), typeof(double));
                if (node.Type == typeof(Decimal?))
                    return Expression.Convert(base.Visit(node.Operand), typeof(double?));
            }

            return node.Update(base.Visit(node.Operand));
        }
    }
}
