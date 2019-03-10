using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Test
{
    public readonly struct OpenTypeConverter
    {
        private readonly IReadOnlyList<EfInclude> _includes;
        private readonly Dictionary<Object, Object> _visited;
        private readonly HashSet<Object> _visitedRecursive;
        public static readonly String NotSetString = Guid.NewGuid().ToString();

        public OpenTypeConverter(IReadOnlyList<EfInclude> includes)
        {
            _includes = includes;
            _visited = new Dictionary<Object, Object>();
            _visitedRecursive = new HashSet<Object>();
        }

        public IList Convert(IEnumerable entities)
        {
            IList list = ToOpenType(entities);
            for (int i = 0; i < list.Count; i++)
                UpdateRecursive(list[i]);
            return list;
        }
        private Object FilterCollection(Object value, PropertyInfo property)
        {
            if (_includes == null)
                return value;

            EfInclude include;
            if (property.DeclaringType.Name.StartsWith("<>"))
                include = _includes.FirstOrDefault(i => i.Property.Name == property.Name);
            else
                include = _includes.FirstOrDefault(i => i.Property == property);
            if (include.Property == null)
                return null;

            if (include.Filter == null)
                return value;

            return include.Filter((IEnumerable)value);
        }
        private IList ToOpenType(IEnumerable entities)
        {
            var openTypes = new List<Object>();
            foreach (Object entity in entities)
                openTypes.Add(ToOpenType(entity));
            return openTypes;
        }
        private Object ToOpenType(Object entity)
        {
            if (entity == null)
                return null;

            if (_visited.ContainsKey(entity))
                return _visited[entity];

            if (entity is IReadOnlyDictionary<String, Object> dictionary)
            {
                var openType = new SortedDictionary<String, Object>(StringComparer.Ordinal);
                foreach (KeyValuePair<String, Object> pair in dictionary)
                {
                    Object value = pair.Value;
                    if (value is Type)//navigation property null value
                        continue;

                    if (value is Decimal d)
                        value = Math.Round(d, 2);

                    openType.Add(pair.Key, ToOpenType(value));
                }
                return openType;
            }

            Type type = Parsers.OeExpressionHelper.GetCollectionItemType(entity.GetType());
            if (type == null)
            {
                if (Parsers.OeExpressionHelper.IsEntityType(entity.GetType()))
                {
                    Object notSetEntity = null;
                    if (!entity.GetType().Name.StartsWith("<>"))
                        notSetEntity = Activator.CreateInstance(entity.GetType());

                    var openType = new SortedDictionary<String, Object>(StringComparer.Ordinal);
                    foreach (PropertyInfo property in entity.GetType().GetProperties())
                    {
                        type = Parsers.OeExpressionHelper.GetCollectionItemType(property.PropertyType);
                        Object value = property.GetValue(entity);
                        if (value == null)
                        {
                            if (type == null)
                                type = property.PropertyType;

                            if (!Parsers.OeExpressionHelper.IsEntityType(type))
                                openType.Add(property.Name, null);
                        }
                        else
                        {
                            if (type == null)
                            {
                                if (Parsers.OeExpressionHelper.IsEntityType(property.PropertyType))
                                {
                                    value = FilterCollection(value, property);
                                    if (value == null)
                                        continue;

                                    _visited[entity] = value;
                                    value = ToOpenType(value);
                                    _visited[entity] = value;
                                }
                                else
                                {
                                    var comparable = (IComparable)value;
                                    if (notSetEntity != null && comparable.CompareTo(property.GetValue(notSetEntity)) == 0)
                                        continue;

                                    if (value is Decimal d)
                                        value = Math.Round(d, 2);
                                }
                            }
                            else
                            {
                                if (Parsers.OeExpressionHelper.IsEntityType(type))
                                {
                                    value = FilterCollection(value, property);
                                    if (value == null)
                                        continue;

                                    value = ToOpenType((IEnumerable)value);
                                }
                            }
                            openType.Add(property.Name, value);
                        }
                    }

                    _visited[entity] = openType;
                    return openType;
                }

                return entity;
            }

            if (Parsers.OeExpressionHelper.IsEntityType(type))
                return ToOpenType((IEnumerable)entity);

            return entity;

        }
        private void UpdateRecursive(Object value)
        {
            if (!_visitedRecursive.Add(value))
                return;

            if (value is SortedDictionary<String, Object> openType)
                foreach (KeyValuePair<String, Object> pair in new List<KeyValuePair<String, Object>>(openType))
                {
                    if (pair.Value is SortedDictionary<String, Object> dictionary)
                        foreach (KeyValuePair<String, Object> child in dictionary)
                            UpdateRecursive(pair.Value);
                    else if (pair.Value is List<Object> list)
                    {
                        if (list.Count == 0)
                            openType.Remove(pair.Key);
                        else
                            for (int i = 0; i < list.Count; i++)
                            {
                                if (_visited.ContainsKey(list[i]))
                                    list[i] = _visited[list[i]];
                                UpdateRecursive(list[i]);
                            }
                    }
                }
        }
    }
}
