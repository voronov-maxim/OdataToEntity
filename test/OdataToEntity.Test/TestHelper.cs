using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test
{
    public sealed class IncludeVisitor : ExpressionVisitor
    {
        public struct Include
        {
            public readonly Delegate Lambda;
            public readonly PropertyInfo Property;

            public Include(PropertyInfo lambda, Delegate predicate)
            {
                Property = lambda;
                Lambda = predicate;
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
                return _lambda = node;
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
            return node;
        }

        public IReadOnlyList<Include> Includes => _includes;
    }

    internal static partial class TestHelper
    {
        private static bool IsCollection(Type collectionType)
        {
            if (collectionType.GetTypeInfo().IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;

            foreach (Type iface in collectionType.GetTypeInfo().GetInterfaces())
                if (iface.GetTypeInfo().IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return true;

            return false;
        }
        public static bool IsEntity(Type type)
        {
            TypeInfo typeInfo = type.GetTypeInfo();
            if (typeInfo.IsPrimitive)
                return false;
            if (typeInfo.IsValueType)
                return false;
            if (type == typeof(String))
                return false;
            return true;
        }
        public static void SetNullCollection(IList rootItems, IEnumerable<IncludeVisitor.Include> includes)
        {
            var visited = new HashSet<Object>();
            var includesDistinct = new Dictionary<PropertyInfo, Delegate>();
            foreach (IncludeVisitor.Include include in includes)
                includesDistinct[include.Property] = include.Lambda;

            foreach (Object root in rootItems)
                SetNullCollection(root, visited, includesDistinct);
        }
        private static void SetNullCollection(Object entity, HashSet<Object> visited, Dictionary<PropertyInfo, Delegate> includes)
        {
            if (entity == null || visited.Contains(entity))
                return;

            visited.Add(entity);
            foreach (PropertyInfo property in entity.GetType().GetProperties())
                if (IsEntity(property.PropertyType))
                {
                    Object value = property.GetValue(entity);
                    if (value == null)
                        continue;

                    if (!includes.ContainsKey(property))
                        if (property.CanWrite)
                        {
                            property.SetValue(entity, null);
                            continue;
                        }

                    if (IsCollection(property.PropertyType))
                    {
                        bool isEmpty = true;
                        foreach (Object item in (IEnumerable)value)
                        {
                            isEmpty = false;
                            SetNullCollection(item, visited, includes);
                        }
                        if (isEmpty)
                            property.SetValue(entity, null);
                        else
                        {
                            Delegate lambda = includes[property];
                            if (lambda != null)
                            {
                                IList list = Lambda((IEnumerable)value, lambda);
                                if (list.Count == 0)
                                    property.SetValue(entity, null);
                                else
                                    property.SetValue(entity, list);
                            }
                        }
                    }
                    else
                        SetNullCollection(value, visited, includes);
                }
        }
        private static IList Lambda(IEnumerable source, Delegate lambda)
        {
            return LambdaT((dynamic)source, lambda);
        }
        private static IList LambdaT<T>(IEnumerable<T> source, Delegate lambda)
        {
            var predicate = lambda as Func<T, bool>;
            if (predicate == null)
                return LambdaOrderByDesc(source, (dynamic)lambda);
            return LambdaWhere<T>(source, (dynamic)lambda);
        }
        private static IList<T> LambdaOrderByDesc<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
        {
            return source.OrderByDescending(keySelector).ToArray();
        }
        private static IList<T> LambdaWhere<T>(IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.Where(predicate).ToArray();
        }
    }
}
