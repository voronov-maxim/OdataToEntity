using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace OdataToEntity.Test
{
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
        public static void SetNullCollection(IList rootItems)
        {
            var visited = new HashSet<Object>();
            foreach (Object root in rootItems)
                SetNullCollection(root, visited, true);
        }
        private static void SetNullCollection(Object entity, HashSet<Object> visited, bool isRoot)
        {
            if (entity == null || visited.Contains(entity))
                return;

            visited.Add(entity);
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(entity))
                if (IsEntity(property.PropertyType))
                {
                    Object value = property.GetValue(entity);
                    if (value == null)
                        continue;

                    if (IsCollection(property.PropertyType))
                    {
                        if (isRoot)
                        {
                            bool isEmpty = true;
                            foreach (Object item in (IEnumerable)value)
                            {
                                isEmpty = false;
                                SetNullCollection(item, visited, false);
                            }
                            if (isEmpty)
                                property.SetValue(entity, null);
                        }
                        else
                            property.SetValue(entity, null);
                    }
                    else
                    {
                        if (isRoot)
                            SetNullCollection(value, visited, false);
                        else
                            property.SetValue(entity, null);
                    }
                }
        }
    }
}
