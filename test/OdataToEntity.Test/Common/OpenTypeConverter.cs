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
        private readonly List<PropertyInfo> _navigationProperties;
        private readonly Dictionary<Object, Object> _visited;
        public static readonly String NotSetString = Guid.NewGuid().ToString();

        public OpenTypeConverter(IReadOnlyList<EfInclude> includes)
        {
            _includes = includes;
            _navigationProperties = new List<PropertyInfo>();
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
        private EfInclude FindInclude(PropertyInfo property)
        {
            List<PropertyInfo> navigationProperties;
            if (property == null)
            {
                if (_navigationProperties.Count == 0)
                    return default;

                navigationProperties = _navigationProperties;
            }
            else
                navigationProperties = new List<PropertyInfo>(_navigationProperties) { property };

            for (int i = 0; i < _includes.Count; i++)
            {
                EfInclude include = _includes[i];
                int j = navigationProperties.Count - 1;
                do
                {
                    if (include.Property.Name != navigationProperties[j].Name)
                        break;

                    if (include.ParentProperty == null && j == 0)
                        return _includes[i];

                    include = _includes.FirstOrDefault(n => n.Property == include.ParentProperty);
                    j--;
                }
                while (include.Property != null && j >= 0);
            }

            return default;
        }
        private Object FilterNavigation(Object value, PropertyInfo property)
        {
            EfInclude matched = FindInclude(property);
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
        private static Object OrderKeySelector(IReadOnlyDictionary<String, Object> value)
        {
            if (value.ContainsKey("Id"))
                return value["Id"];

            Object firstValue = value.Values.First();
            if (firstValue is IComparable comparable)
                return comparable;

            if (firstValue is IReadOnlyDictionary<String, Object> dictionary)
                return dictionary.Values.First();

            return 0;
        }
        private IList ToOpenType(IEnumerable entities)
        {
            var openTypes = new List<Object>();
            foreach (Object entity in entities)
            {
                Object value = ToOpenType(entity);
                if (value != null)
                    openTypes.Add(value);
            }

            if (openTypes.Count == 0)
                return null;

            if (_navigationProperties.Count > 0)
            {
                EfInclude matched = FindInclude(null);
                if (matched.Property != null && !matched.IsOrdered)
                    openTypes = new List<Object>(openTypes.Cast<IReadOnlyDictionary<String, Object>>().OrderBy(OrderKeySelector));
            }

            return openTypes;
        }
        public Object ToOpenType(Object entity)
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
                    if (pair.Key == "Dummy")
                        continue;

                    Object value = pair.Value;
                    if (value == DBNull.Value)//navigation property null value
                        continue;

                    if (value is Decimal d)
                        value = Math.Round(d * 1.00M, 2);

                    if (value is DateTimeOffset dateTimeOffset)
                        value = dateTimeOffset.ToUniversalTime();

                    if (value is ICollection collection && collection.Count == 0)
                        continue;

                    _navigationProperties.Add(new Infrastructure.OeShadowPropertyInfo(typeof(Object), typeof(Object), pair.Key));
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
                                value = FilterNavigation(value, property);
                                if (value == null)
                                    continue;

                                _navigationProperties.Add(property);
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
                                    value = Math.Round(d * 1.00M, 2);

                                if (value is DateTimeOffset dateTimeOffset)
                                    value = dateTimeOffset.ToUniversalTime();

                            }
                            openType.Add(property.Name, value);
                        }
                    }

                    _visited[entity] = openType;
                    return openType.Count == 0 ? null : openType;
                }

                return entity;
            }

            if (Parsers.OeExpressionHelper.IsEntityType(type))
                return ToOpenType((IEnumerable)entity);

            return entity;
        }
    }
}
