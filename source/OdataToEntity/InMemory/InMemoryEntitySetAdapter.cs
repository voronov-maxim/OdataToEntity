using Microsoft.OData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace OdataToEntity.InMemory
{
    internal sealed class InMemoryEntitySetAdapter : Db.OeEntitySetAdapter
    {
        private readonly Type _clrEntityType;
        private readonly Func<Object, Object[], bool> _comparer;
        private readonly String[] _keyNames;
        private PropertyInfo? _sourcePropertyInfo;
        private Func<IEnumerable, Object, bool>? _tryAddToCollection;

        public InMemoryEntitySetAdapter(Type clrEntityType, String entitySetName, IReadOnlyList<PropertyInfo> keys)
        {
            _clrEntityType = clrEntityType;
            EntitySetName = entitySetName;
            IsDbQuery = keys.Count == 0;

            _keyNames = keys.Select(p => p.Name).ToArray();
            if (keys.Count == 0)
                _comparer = (e, k) => throw new NotSupportedException("Query not support find by key");
            else
                _comparer = CreateEntityComparer(clrEntityType, keys);
        }

        public override void AddEntity(Object dataContext, ODataResourceBase entry)
        {
            Object entity = CreateEntity(entry);
            IEnumerable source = GetSource(dataContext);
            if (source is IList list)
            {
                lock (list)
                    list.Add(entity);
                return;
            }

            if (TryAddToCollection(source, entity))
                return;

            throw new InvalidOperationException("Cannot add entity if source is not list");
        }
        public override void AttachEntity(Object dataContext, ODataResourceBase entry)
        {
            Object entity = FindEntity(GetSource(dataContext), entry);
            lock (entity)
                foreach (ODataProperty property in entry.Properties)
                    if (Array.IndexOf(_keyNames, property.Name) == -1)
                    {
                        PropertyInfo propertyInfo = EntityType.GetProperty(property.Name)!;
                        Object clrValue = OeEdmClrHelper.GetClrValue(propertyInfo.PropertyType, property.Value);
                        propertyInfo.SetValue(entity, clrValue);
                    }
        }
        private Object CreateEntity(ODataResourceBase entry)
        {
            Object entity = Activator.CreateInstance(EntityType)!;
            foreach (ODataProperty odataProperty in entry.Properties)
            {
                PropertyInfo? property = EntityType.GetProperty(odataProperty.Name);
                if (property == null)
                    throw new InvalidOperationException("Cannot find property " + odataProperty.Name + " in entity " + EntityType.FullName);

                Object value = OeEdmClrHelper.GetClrValue(property.PropertyType, odataProperty.Value);
                property.SetValue(entity, value);
            }
            return entity;
        }
        private static Func<Object, Object[], bool> CreateEntityComparer(Type entityType, IReadOnlyList<PropertyInfo> keys)
        {
            ConditionalExpression? expression = null;

            ParameterExpression entityParameter = Expression.Parameter(typeof(Object));
            ParameterExpression keysParameter = Expression.Parameter(typeof(Object[]));
            UnaryExpression entity = Expression.Convert(entityParameter, entityType);
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                PropertyInfo key = keys[i];
                MemberExpression property = Expression.Property(entity, key);
                MethodInfo? equalsMethodInfo = property.Type.GetMethod("Equals", new[] { property.Type });
                if (equalsMethodInfo == null)
                    throw new InvalidOperationException("Key property type must implement method Equals");

                BinaryExpression keyValue = Expression.ArrayIndex(keysParameter, Expression.Constant(i));
                MethodCallExpression test = Expression.Call(property, equalsMethodInfo, Expression.Convert(keyValue, property.Type));
                if (expression == null)
                    expression = Expression.Condition(test, Expression.Constant(true), Expression.Constant(false));
                else
                    expression = Expression.Condition(test, expression, Expression.Constant(false));
            }

            var lambda = Expression.Lambda<Func<Object, Object[], bool>>(expression!, entityParameter, keysParameter);
            return lambda.Compile();
        }
        private Object FindEntity(IEnumerable source, ODataResourceBase entry)
        {
            var keyValues = new Object[_keyNames.Length];
            foreach (ODataProperty property in entry.Properties)
            {
                int i = Array.IndexOf(_keyNames, property.Name, 0, _keyNames.Length);
                if (i >= 0)
                    keyValues[i] = property.Value;
            }

            foreach (Object? entity in source)
                if (entity != null && _comparer(entity, keyValues))
                    return entity;

            throw new InvalidOperationException("EntitySet " + EntitySetName + " not found keys (" + String.Join(",", keyValues.Select(k => k?.ToString())) + ")");
        }
        private int FindIndex(IList list, ODataResourceBase entry)
        {
            var keyValues = new Object[_keyNames.Length];
            foreach (ODataProperty property in entry.Properties)
            {
                int i = Array.IndexOf(_keyNames, property.Name, 0, _keyNames.Length);
                if (i >= 0)
                    keyValues[i] = property.Value;
            }

            for (int i = 0; i < list.Count; i++)
            {
                Object? entity = list[i];
                if (entity != null && _comparer(entity, keyValues))
                    return i;
            }

            throw new InvalidOperationException("EntitySet " + EntitySetName + " not found keys (" + String.Join(",", keyValues.Select(k => k?.ToString())) + ")");
        }
        public override IQueryable GetEntitySet(Object dataContext)
        {
            IEnumerable? source = GetSourceOrNull(dataContext);
            if (source == null)
                source = Array.CreateInstance(EntityType, 0);

            return new OeInMemoryQueryableWrapper(source, EntityType);
        }
        private IEnumerable GetSource(Object dataContext)
        {
            IEnumerable? source = GetSourceOrNull(dataContext);
            return source ?? throw new InvalidOperationException("Source is null");
        }
        private IEnumerable? GetSourceOrNull(Object dataContext)
        {
            PropertyInfo? sourcePropertyInfo = Volatile.Read(ref _sourcePropertyInfo);
            if (sourcePropertyInfo == null)
            {
                sourcePropertyInfo = dataContext.GetType().GetProperty(EntitySetName)!;
                Volatile.Write(ref _sourcePropertyInfo, sourcePropertyInfo);
            }
            return (IEnumerable?)sourcePropertyInfo.GetValue(dataContext);
        }
        public override void RemoveEntity(Object dataContext, ODataResourceBase entry)
        {
            var keyValues = new Object[_keyNames.Length];
            foreach (ODataProperty property in entry.Properties)
            {
                int i = Array.IndexOf(_keyNames, property.Name, 0, _keyNames.Length);
                keyValues[i] = property.Value;
            }

            IEnumerable source = GetSource(dataContext);
            if (source is IList list)
            {
                lock (list)
                    list.RemoveAt(FindIndex(list, entry));
            }
            else
                throw new InvalidOperationException("Can only remove from the list");
        }
        private bool TryAddToCollection(IEnumerable source, Object entity)
        {
            Func<IEnumerable, Object, bool>? tryAddToCollection = Volatile.Read(ref _tryAddToCollection);
            if (tryAddToCollection == null)
            {
                Func<IEnumerable, Object, bool> func = TryAddToCollection<Object>;
                MethodInfo methodInfo = func.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(entity.GetType());
                tryAddToCollection = (Func<IEnumerable, Object, bool>)methodInfo.CreateDelegate(typeof(Func<IEnumerable, Object, bool>));
                Volatile.Write(ref _tryAddToCollection, tryAddToCollection);
            }

            return tryAddToCollection(source, entity);
        }
        private static bool TryAddToCollection<T>(IEnumerable source, T entity)
        {
            if (source is ICollection<T> collection)
            {
                lock (collection)
                    collection.Add(entity);
                return true;
            }

            return false;
        }

        public override Type EntityType => _clrEntityType;
        public override String EntitySetName { get; }
        public override bool IsDbQuery { get; }
    }
}
