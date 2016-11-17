using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntityCore.AspClient
{
    internal sealed class TypeMapperVisitor : ExpressionVisitor
    {
        private sealed class IncludeVisitor : ExpressionVisitor
        {
            private PropertyInfo _includeProperty;

            protected override Expression VisitMember(MemberExpression node)
            {
                _includeProperty = node.Member as PropertyInfo;
                return node;
            }

            public static PropertyInfo GetIncludeProperty(Expression e)
            {
                var visitor = new IncludeVisitor();
                visitor.Visit(e);
                return visitor._includeProperty;
            }
        }

        private readonly List<PropertyInfo> _includeProperties;
        private readonly IQueryable _query;
        private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters;
        private Expression _source;

        public TypeMapperVisitor(IQueryable query)
        {
            _query = query;
            _includeProperties = new List<PropertyInfo>();
            _parameters = new Dictionary<ParameterExpression, ParameterExpression>();
        }

        private MemberInfo Map(MemberInfo source)
        {
            Type clientType = Map(source.DeclaringType);
            return clientType == null ? source : clientType.GetMember(source.Name)[0];
        }
        private MethodInfo Map(MethodInfo source)
        {
            if (source == null)
                return null;
            if (!source.IsGenericMethod)
                return source;

            Type[] arguments = Map(source.GetGenericArguments());
            return source.GetGenericMethodDefinition().MakeGenericMethod(arguments);
        }
        private Type Map(Type source)
        {
            if (source.IsPrimitive || source == typeof(String))
                return source;

            if (source.IsGenericType)
            {
                Type[] clientTypes = Map(source.GetGenericArguments());
                return source.GetGenericTypeDefinition().MakeGenericType(clientTypes);
            }

            return TypeMap(source) ?? source;
        }
        private Type[] Map(Type[] source)
        {
            var target = new Type[source.Length];
            for (int i = 0; i < source.Length; i++)
                target[i] = Map(source[i]);
            return target;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            Expression left = base.Visit(node.Left);
            Expression right = base.Visit(node.Right);
            LambdaExpression lambda = base.VisitAndConvert<LambdaExpression>(node.Conversion, "VisitBinary");
            return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method, lambda);
        }
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Expression body = base.Visit(node.Body);
            ReadOnlyCollection<ParameterExpression> parameters = base.VisitAndConvert<ParameterExpression>(node.Parameters, "VisitLambda");

            Type[] clientTypeArguments = Map(typeof(T).GetGenericArguments());
            Type delegateType = typeof(T).GetGenericTypeDefinition().MakeGenericType(clientTypeArguments);
            return Expression.Lambda(delegateType, body, node.Name, node.TailCall, parameters);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            Expression expression = base.Visit(node.Expression);
            return Expression.MakeMemberAccess(expression, Map(node.Member));
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Enumerable) ||
                node.Method.DeclaringType == typeof(Queryable))
            {
                Expression instance = base.Visit(node.Object);
                ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
                return Expression.Call(instance, Map(node.Method), arguments);
            }
            else if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include))
                {
                    PropertyInfo includeProperty = IncludeVisitor.GetIncludeProperty(node.Arguments[1]);
                    _includeProperties.Add(includeProperty);

                    Expression instance = base.Visit(node.Object);
                    ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);

                    Type itemType = arguments[0].Type.GetGenericArguments()[0];
                    Type dataServiceQueryType = typeof(DataServiceQuery<>).MakeGenericType(itemType);
                    MethodInfo expandMethod = dataServiceQueryType.GetMethod("Expand", new[] { typeof(String) });
                    instance = Expression.Convert(arguments[0], dataServiceQueryType);
                    return Expression.Call(instance, expandMethod, Expression.Constant(includeProperty.Name));
                }
                else
                    throw new NotSupportedException("The method '" + node.Method.Name + "' is not supported");
            }

            node = (MethodCallExpression)base.VisitMethodCall(node);
            if (node.Method.Name == "GetValueOrDefault")
            {
                Type underlyingType = Nullable.GetUnderlyingType(node.Object.Type);
                if (underlyingType != null)
                    return Expression.Property(node.Object, "Value");
            }
            return node;
        }
        protected override Expression VisitNew(NewExpression node)
        {
            ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
            ConstructorInfo ctor = Map(node.Type).GetConstructors().Single(c => c.GetParameters().Length == arguments.Count);
            return Expression.New(ctor, arguments);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_source == null && typeof(IQueryable).IsAssignableFrom(node.Type))
            {
                _source = _query.Expression;
                return _source;
            }

            ParameterExpression parameter;
            if (_parameters.TryGetValue(node, out parameter))
                return parameter;

            parameter = Expression.Parameter(Map(node.Type), node.Name);
            _parameters.Add(node, parameter);
            return parameter;
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            Expression operand = base.Visit(node.Operand);
            Type type = Map(node.Type);
            MethodInfo method = Map(node.Method);
            return Expression.MakeUnary(node.NodeType, operand, type, method);
        }

        public IReadOnlyList<PropertyInfo> IncludeProperties => _includeProperties;
        public Func<Type, Type> TypeMap { get; set; }
    }
}
