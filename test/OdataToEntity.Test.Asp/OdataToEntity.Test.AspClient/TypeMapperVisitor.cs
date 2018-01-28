using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test.Model
{
    internal sealed class TypeMapperVisitor : ExpressionVisitor
    {
        private readonly IQueryable _query;
        private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters;
        private Expression _source;

        public TypeMapperVisitor(IQueryable query)
        {
            _query = query;
            _parameters = new Dictionary<ParameterExpression, ParameterExpression>();
        }

        private MemberInfo Map(MemberInfo source)
        {
            if (source.DeclaringType == typeof(DateTime))
                return typeof(DateTimeOffset).GetMember(source.Name)[0];

            Type clientType = Map(source.DeclaringType);
            return clientType == null ? source : clientType.GetMember(source.Name)[0];
        }
        private MethodInfo Map(MethodInfo source)
        {
            if (source == null)
                return null;

            if (source.DeclaringType == typeof(DateTime))
                return typeof(DateTimeOffset).GetMethod(source.Name);

            if (!source.IsGenericMethod)
                return source;

            Type[] arguments = Map(source.GetGenericArguments());
            return source.GetGenericMethodDefinition().MakeGenericMethod(arguments);
        }
        private Type Map(Type source)
        {
            if (source == typeof(String))
                return source;

            if (source.IsPrimitive)
                return source == typeof(DateTime) ? typeof(DateTimeOffset) : source;

            if (source.IsGenericType)
            {
                if (source == typeof(DateTime?))
                    return typeof(DateTimeOffset?);

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

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Type mapType = Map(node.Type);
            if (node.Value != null && mapType.IsEnum)
            {
                Object value = Enum.Parse(mapType, node.Value.ToString());
                return Expression.Constant(value, mapType);
            }

            return mapType == node.Type ? node : Expression.Constant(node.Value, mapType);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            Expression left = base.Visit(node.Left);
            Expression right = base.Visit(node.Right);
            LambdaExpression lambda = base.VisitAndConvert<LambdaExpression>(node.Conversion, "VisitBinary");
            return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, Map(node.Method), lambda);
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
                    ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
                    Type itemType = arguments[0].Type.GetGenericArguments()[0];
                    Type dataServiceQueryType = typeof(DataServiceQuery<>).MakeGenericType(itemType);
                    var arg1 = (UnaryExpression)arguments[1];

                    MethodInfo expandMethod = GetExpandMethodInfo((dynamic)_query, (dynamic)arg1.Operand);
                    Expression instance = Expression.Convert(arguments[0], dataServiceQueryType);
                    return Expression.Call(instance, expandMethod, arg1);
                }
                else
                    throw new NotSupportedException("The method '" + node.Method.Name + "' is not supported");
            }

            if (node.Method.Name == "GetValueOrDefault")
            {
                Expression e = base.Visit(node.Object);
                return Expression.Property(e, "Value");
            }
            return base.VisitMethodCall(node);
        }
        private static MethodInfo GetExpandMethodInfo<TElement, TTarget>(DataServiceQuery<TElement> dsq, Expression<Func<TElement, TTarget>> navigationPropertyAccessor)
        {
            Func<Expression<Func<TElement, TTarget>>, DataServiceQuery<TElement>> expand = dsq.Expand<TTarget>;
            return expand.GetMethodInfo();
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

        public Func<Type, Type> TypeMap { get; set; }
    }
}
