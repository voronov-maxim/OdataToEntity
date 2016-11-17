using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Test
{
    internal static partial class TestHelper
    {
        private sealed class IncludeVisitor : ExpressionVisitor
        {
            private sealed class IncludePropertyVisitor : ExpressionVisitor
            {
                private PropertyInfo _includeProperty;

                protected override Expression VisitMember(MemberExpression node)
                {
                    _includeProperty = node.Member as PropertyInfo;
                    return node;
                }

                public static PropertyInfo GetIncludeProperty(Expression e)
                {
                    var visitor = new IncludePropertyVisitor();
                    visitor.Visit(e);
                    return visitor._includeProperty;
                }
            }

            private readonly List<PropertyInfo> _includeProperties;

            public IncludeVisitor()
            {
                _includeProperties = new List<PropertyInfo>();
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                base.Visit(node.Arguments[0]);

                if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
                {
                    if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include) ||
                        node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
                    {
                        PropertyInfo includeProperty = IncludePropertyVisitor.GetIncludeProperty(node.Arguments[1]);
                        _includeProperties.Add(includeProperty);
                    }
                }
                return node;
            }

            public IReadOnlyList<PropertyInfo> IncludeProperties => _includeProperties;
        }

        public static IReadOnlyList<PropertyInfo> GetIncludeProperties(Expression expression)
        {
            var visitor = new IncludeVisitor();
            visitor.Visit(expression);
            return visitor.IncludeProperties;
        }
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
        public static void SetNullCollection(IList rootItems, IEnumerable<PropertyInfo> includeProperties)
        {
            var visited = new HashSet<Object>();
            foreach (Object root in rootItems)
                SetNullCollection(root, visited, new HashSet<PropertyInfo>(includeProperties));
        }
        private static void SetNullCollection(Object entity, HashSet<Object> visited, HashSet<PropertyInfo> includeProperties)
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

                    if (!includeProperties.Contains(property))
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
                            SetNullCollection(item, visited, includeProperties);
                        }
                        if (isEmpty)
                            property.SetValue(entity, null);
                    }
                    else
                        SetNullCollection(value, visited, includeProperties);
                }
        }
    }
}
