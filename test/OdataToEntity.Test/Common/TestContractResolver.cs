using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.Test
{
    internal sealed class TestContractResolver : DefaultContractResolver
    {
        private sealed class EmptyCollectionValueProvider : IValueProvider
        {
            private readonly IValueProvider _defaultValueProvider;
            private readonly Func<IEnumerable, IList> _lambda;

            public EmptyCollectionValueProvider(IValueProvider defaultValueProvider, Func<IEnumerable, IList> lambda)
            {
                _defaultValueProvider = defaultValueProvider;
                _lambda = lambda;
            }
            public Object GetValue(Object target)
            {
                var items = (IEnumerable)_defaultValueProvider.GetValue(target);
                if (items == null)
                    return null;

                if (_lambda == null)
                    return items.GetEnumerator().MoveNext() ? items : null;

                IList list = _lambda(items);
                return list.Count == 0 ? null : list;
            }

            public void SetValue(Object target, Object value) => throw new NotSupportedException();
        }

        private sealed class FixDecimalValueConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => true;
            public override Object ReadJson(JsonReader reader, Type ObjectType, Object existingValue, JsonSerializer serializer) => throw new NotSupportedException();
            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                if (value is Decimal d)
                    value = Math.Round(d, 2);
                serializer.Serialize(writer, value);
            }
        }

        private sealed class NullValueProvider : IValueProvider
        {
            public static NullValueProvider Instance = new NullValueProvider();

            private NullValueProvider() { }

            public Object GetValue(Object target) => null;
            public void SetValue(Object target, Object value) => throw new NotSupportedException();
        }

        private readonly Dictionary<PropertyInfo, Func<IEnumerable, IList>> _includes;

        public TestContractResolver(IReadOnlyList<IncludeVisitor.Include> includes)
        {
            _includes = new Dictionary<PropertyInfo, Func<IEnumerable, IList>>();
            if (includes != null)
                foreach (IncludeVisitor.Include include in includes)
                    _includes[include.Property] = include.Filter;
        }

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            JsonDictionaryContract dictionaryContract = base.CreateDictionaryContract(objectType);
            dictionaryContract.ItemConverter = new FixDecimalValueConverter();
            return dictionaryContract;
        }
        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            return base.CreateMemberValueProvider(member);
        }
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> jproperties = base.CreateProperties(type, memberSerialization);
            if (IsEntity(type))
                foreach (JsonProperty jproperty in jproperties)
                {
                    PropertyInfo clrProperty = type.GetProperty(jproperty.PropertyName);

                    if (_includes.TryGetValue(clrProperty, out Func<IEnumerable, IList> lambda))
                    {
                        if (typeof(IEnumerable).IsAssignableFrom(clrProperty.PropertyType))
                            jproperty.ValueProvider = new EmptyCollectionValueProvider(jproperty.ValueProvider, lambda);
                    }
                    else
                    {
                        if (IsEntity(clrProperty.PropertyType))
                            jproperty.ValueProvider = NullValueProvider.Instance;
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
    }
}
