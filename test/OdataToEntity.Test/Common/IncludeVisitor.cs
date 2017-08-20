using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test
{
    internal sealed class IncludeVisitor : ExpressionVisitor
    {
        public struct Include
        {
            public readonly Delegate Lambda;
            public readonly PropertyInfo Property;

            public Include(PropertyInfo property, Delegate lambda)
            {
                Property = property;
                Lambda = lambda;
            }
        }

        private sealed class IncludePropertyVisitor : ExpressionVisitor
        {
            private LambdaExpression _lambda;
            private ParameterExpression _parameter;
            private MemberExpression _property;

            protected override Expression VisitMember(MemberExpression node)
            {
                if (_property == null)
                    _property = node;
                return node;
            }
            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                if (_property == null)
                {
                    _parameter = node.Parameters[0];
                    return base.VisitLambda<T>(node);
                }
                return node;
            }
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                ParameterExpression source = Expression.Parameter(node.Arguments[0].Type);
                MethodCallExpression call = Expression.Call(null, node.Method, source, node.Arguments[1]);
                _lambda = Expression.Lambda(call, source);

                return base.VisitMethodCall(node);
            }

            public LambdaExpression Lambda => _lambda;
            public ParameterExpression Parameter => _parameter;
            public MemberExpression Property => _property;
        }

        private readonly List<Include> _includes;

        public IncludeVisitor()
        {
            _includes = new List<Include>();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression expression = base.Visit(node.Arguments[0]);

            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include) ||
                    node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                {
                    var visitor = new IncludePropertyVisitor();
                    visitor.Visit(node.Arguments[1]);

                    if (visitor.Lambda == null)
                        node = Expression.Call(null, node.Method, new Expression[] { expression, node.Arguments[1] });
                    else
                    {
                        if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include))
                        {
                            Type entityType = node.Method.GetGenericArguments()[0];
                            MethodInfo method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, visitor.Property.Type);
                            LambdaExpression lambda = Expression.Lambda(visitor.Property, visitor.Parameter);
                            node = Expression.Call(null, method, new Expression[] { node.Arguments[0], lambda });
                        }
                        else
                        {
                            Type entityType = node.Method.GetGenericArguments()[0];
                            Type previousPropertyType = node.Method.GetGenericArguments()[1];
                            MethodInfo method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(entityType, previousPropertyType, visitor.Property.Type);
                            LambdaExpression lambda = Expression.Lambda(visitor.Property, visitor.Parameter);
                            node = Expression.Call(null, method, new Expression[] { node.Arguments[0], lambda });
                        }
                    }

                    Delegate predicate = visitor.Lambda == null ? null : visitor.Lambda.Compile();
                    _includes.Add(new Include(visitor.Property.Member as PropertyInfo, predicate));
                }
            }
            else
                if (node.Arguments.Count == 1)
                node = Expression.Call(node.Object, node.Method, expression);
            else
                    node = Expression.Call(node.Object, node.Method, expression, node.Arguments[1]);
            return node;
        }

        public IReadOnlyList<Include> Includes => _includes;
    }
}
