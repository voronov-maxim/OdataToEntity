using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Test
{
    internal sealed class TestContractResolver : DefaultContractResolver
    {
        private sealed class EmptyCollectionValueProvider : IValueProvider
        {
            private readonly TestContractResolver _contractResolver;
            private readonly IValueProvider _defaultValueProvider;
            private readonly Func<IEnumerable, IList> _lambda;

            public EmptyCollectionValueProvider(TestContractResolver contractResolver, IValueProvider defaultValueProvider, Func<IEnumerable, IList> lambda)
            {
                _contractResolver = contractResolver;
                _defaultValueProvider = defaultValueProvider;
                _lambda = lambda;
            }
            public Object GetValue(Object target)
            {
                var items = (IEnumerable)_defaultValueProvider.GetValue(target);
                if (items == null)
                    return null;

                if (_lambda == null || _contractResolver.DisableWhereOrder)
                    return items;

                return _lambda(items);
            }

            public void SetValue(Object target, Object value) => throw new NotSupportedException();
        }

        private sealed class DictionaryValueConverter : JsonConverter
        {
            private readonly ModelBuilder.OeEdmModelMetadataProvider _metadataProvider;

            public DictionaryValueConverter(ModelBuilder.OeEdmModelMetadataProvider metadataProvider)
            {
                _metadataProvider = metadataProvider;
            }

            public override bool CanConvert(Type objectType) => true;
            public override Object ReadJson(JsonReader reader, Type ObjectType, Object existingValue, JsonSerializer serializer) => throw new NotSupportedException();
            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                if (value is Decimal d)
                    value = Math.Round(d, 2);
                else if (value is IReadOnlyCollection<Object> collection)
                    value = FillArray(collection);
                else if (value is IEnumerable enumerable && !(value is String))
                {
                    var items = new List<Object>();
                    foreach (Object item in enumerable)
                        items.Add(item);

                    value = FillArray(items);
                }
                serializer.Serialize(writer, value);

                Array FillArray(IReadOnlyCollection<Object> collection)
                {
                    if (collection.Count == 0)
                        return null;

                    Type entityType = collection.First().GetType();
                    Array items = Array.CreateInstance(entityType, collection.Count);
                    int i = 0;
                    foreach (Object item in collection)
                        items.SetValue(item, i++);

                    if (!entityType.Name.StartsWith("<>"))
                    {
                        IComparer comparer = GetComparer(_metadataProvider, entityType);
                        Array.Sort(items, comparer);
                    }
                    return items;
                }
            }
        }

        private sealed class NullEntityValueProvider : IValueProvider
        {
            public static NullEntityValueProvider Instance = new NullEntityValueProvider();

            private NullEntityValueProvider() { }

            public Object GetValue(Object target) => null;
            public void SetValue(Object target, Object value) => throw new NotSupportedException();
        }

        private sealed class NullIdValueProvider : IValueProvider
        {
            private readonly PropertyInfo _propertyInfo;

            public NullIdValueProvider(PropertyInfo propertyInfo)
            {
                _propertyInfo = propertyInfo;
            }

            public Object GetValue(Object target)
            {
                int id = (int)_propertyInfo.GetValue(target);
                return id == 0 ? (Object)null : id;
            }
            public void SetValue(Object target, Object value) => throw new NotSupportedException();
        }

        private static readonly ConcurrentDictionary<Type, IComparer> _entityComaprers = new ConcurrentDictionary<Type, IComparer>();
        private readonly Dictionary<String, Func<IEnumerable, IList>> _includes;
        private readonly ModelBuilder.OeEdmModelMetadataProvider _metadataProvider;

        public TestContractResolver(ModelBuilder.OeEdmModelMetadataProvider metadataProvider, IReadOnlyList<IncludeVisitor.Include> includes)
        {
            _metadataProvider = metadataProvider;

            _includes = new Dictionary<String, Func<IEnumerable, IList>>();
            if (includes != null)
                foreach (IncludeVisitor.Include include in includes)
                    _includes[include.Property.DeclaringType.FullName + "." + include.Property.Name] = include.Filter;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            JsonDictionaryContract dictionaryContract = base.CreateDictionaryContract(objectType);
            dictionaryContract.ItemConverter = new DictionaryValueConverter(_metadataProvider);
            return dictionaryContract;
        }
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> jproperties = base.CreateProperties(type, memberSerialization);
            if (IsEntity(type))
                foreach (JsonProperty jproperty in jproperties)
                {
                    PropertyInfo clrProperty = type.GetProperty(jproperty.PropertyName);
                    String propertyFullName = clrProperty.DeclaringType.FullName + "." + clrProperty.Name;

                    if (_includes.TryGetValue(propertyFullName, out Func<IEnumerable, IList> lambda))
                    {
                        if (typeof(IEnumerable).IsAssignableFrom(clrProperty.PropertyType))
                            jproperty.ValueProvider = new EmptyCollectionValueProvider(this, jproperty.ValueProvider, lambda);
                    }
                    else
                    {
                        if (IsEntity(clrProperty.PropertyType))
                            jproperty.ValueProvider = NullEntityValueProvider.Instance;
                        else
                        {
                            if (clrProperty.PropertyType == typeof(int) && clrProperty.Name.EndsWith("Id"))
                                jproperty.ValueProvider = new NullIdValueProvider(clrProperty);
                        }
                    }
                }

            return jproperties.OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
        }
        public static bool IsEntity(Type type)
        {
            if (type.IsPrimitive)
                return false;
            if (type.IsValueType)
                return false;
            if (type == typeof(String))
                return false;
            return true;
        }
        private static IComparer GetComparer(ModelBuilder.OeEdmModelMetadataProvider metadataProvider, Type entityType)
        {
            if (_entityComaprers.TryGetValue(entityType, out IComparer comparer))
                return comparer;

            comparer = TestHelper.CreateEntryEqualityComparer(metadataProvider, entityType);
            _entityComaprers.TryAdd(entityType, comparer);
            return comparer;
        }

        public bool DisableWhereOrder { get; set; }
    }
}
