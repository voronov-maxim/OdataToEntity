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
        private readonly List<String> _navigationProperties;
        private readonly Dictionary<Object, Object> _visited;
        public static readonly String NotSetString = Guid.NewGuid().ToString();

        public OpenTypeConverter(IReadOnlyList<EfInclude> includes)
        {
            _includes = includes;
            _navigationProperties = new List<String>();
            _visited = new Dictionary<Object, Object>();
        }

        private static IList AnonumousToEntity(Type entityType, IEnumerable collection)
        {
            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(collection.GetType());
            var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entityType));
            PropertyInfo[] anonymousProperties = itemType.GetProperties();
            PropertyInfo[] entityProperties = entityType.GetProperties();
            foreach (Object item in (IEnumerable)collection)
            {
                Object entity = Activator.CreateInstance(entityType);
                foreach (PropertyInfo anonymousProperty in anonymousProperties)
                {
                    PropertyInfo entityProperty = Array.Find(entityProperties, p => p.Name == anonymousProperty.Name);
                    entityProperty.SetValue(entity, anonymousProperty.GetValue(item));
                }
                items.Add(entity);
            }
            return items;
        }
        public IList Convert(IEnumerable entities)
        {
            return ToOpenType(entities);
        }
        private Object FilterNavigation(Object value, String propertyName)
        {
            if (_includes == null)
                return value;

            List<EfInclude> includes = _includes.Where(t => t.Property.Name == propertyName).ToList();
            if (includes.Count == 0)
                return null;

            EfInclude matched = default;
            foreach (EfInclude include in includes)
            {
                int i = _navigationProperties.Count - 1;
                PropertyInfo parentProperty = include.ParentProperty;
                while (parentProperty != null && i >= 0)
                {
                    EfInclude parentInclude = _includes.FirstOrDefault(t => t.Property.Name == parentProperty.Name);
                    if (_navigationProperties[i] != parentInclude.Property.Name)
                        break;

                    i--;
                    parentProperty = parentInclude.ParentProperty;
                }
                if (parentProperty == null && i < 0)
                {
                    matched = include;
                    break;
                }
            }

            if (matched.Property == null)
                return null;

            if (matched.Filter == null)
                return value;

            Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(value.GetType());
            if (itemType.Name.StartsWith("<>"))
            {
                Type entityType = Parsers.OeExpressionHelper.GetCollectionItemType(matched.Property.PropertyType);
                value = AnonumousToEntity(entityType, (IEnumerable)value);
            }
            return matched.Filter((IEnumerable)value);
        }
        private IList ToOpenType(IEnumerable entities)
        {
            var openTypes = new List<Object>();
            foreach (Object entity in entities)
            {
                Object value = ToOpenType(entity);
                if (value != null)
                    openTypes.Add(ToOpenType(entity));
            }

            return openTypes.Count == 0 ? null : openTypes;
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

                    if (value is ICollection collection && collection.Count == 0)
                        continue;

                    _navigationProperties.Add(pair.Key);
                    openType.Add(pair.Key, ToOpenType(value));
                    _navigationProperties.RemoveAt(_navigationProperties.Count - 1);
                }
                return openType;
            }

            Type type = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(entity.GetType());
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
                        type = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(property.PropertyType);
                        bool isEntityType;
                        if (type == null)
                        {
                            isEntityType = Parsers.OeExpressionHelper.IsEntityType(property.PropertyType);
                            if (!isEntityType)
                                type = property.PropertyType;
                        }
                        else
                            isEntityType = Parsers.OeExpressionHelper.IsEntityType(type);

                        Object value = property.GetValue(entity);
                        if (value == null)
                        {
                            if (!isEntityType)
                                openType.Add(property.Name, null);
                        }
                        else
                        {
                            if (isEntityType)
                            {
                                value = FilterNavigation(value, property.Name);
                                if (value == null)
                                    continue;

                                _navigationProperties.Add(property.Name);
                                if (type == null)
                                    value = ToOpenType(value);
                                else
                                    value = ToOpenType((IEnumerable)value);
                                _navigationProperties.RemoveAt(_navigationProperties.Count - 1);

                                if (value == null)
                                    continue;
                            }
                            else
                            {
                                var comparable = (IComparable)value;
                                if (notSetEntity != null && comparable.CompareTo(property.GetValue(notSetEntity)) == 0)
                                    continue;

                                if (value is Decimal d)
                                    value = Math.Round(d, 2);
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
    }
}
