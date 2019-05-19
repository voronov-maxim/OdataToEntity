using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public abstract class DynamicType : IDictionary<String, Object>
    {
        private readonly DynamicTypeDefinition _dynamicTypeDefinition;
        private String[] _keys;
        private readonly Dictionary<String, Object> _properties;
        private Object[] _values;

        protected DynamicType()
        {
            throw new InvalidOperationException("Fake constructor");
        }
        protected DynamicType(DynamicTypeDefinition dynamicTypeDefinition)
        {
            _properties = new Dictionary<String, Object>();
            _dynamicTypeDefinition = dynamicTypeDefinition;
        }

        private String[] GetKeys()
        {
            if (_keys == null)
            {
                _keys = new String[_properties.Count + _dynamicTypeDefinition.PropertyNames.Count];
                int i = 0;
                foreach (KeyValuePair<String, Object> pair in _properties)
                    _keys[i++] = pair.Key;
                foreach (String propertyName in _dynamicTypeDefinition.PropertyNames)
                    _keys[i++] = propertyName;
            }
            return _keys;
        }
        private Object[] GetValues()
        {
            if (_values == null)
            {
                _values = new Object[_properties.Count + _dynamicTypeDefinition.PropertyNames.Count];
                int i = 0;
                foreach (KeyValuePair<String, Object> pair in _properties)
                    _values[i++] = pair.Value;
                foreach (String propertyName in _dynamicTypeDefinition.PropertyNames)
                {
                    _dynamicTypeDefinition.TryGetValue(this, propertyName, out Object value);
                    _values[i++] = value;
                }
            }
            return _values;
        }

        void IDictionary<String, Object>.Add(String key, Object value)
        {
            _properties.Add(key, value);
        }
        void ICollection<KeyValuePair<String, Object>>.Add(KeyValuePair<String, Object> item)
        {
            _properties.Add(item.Key, item.Value);
        }
        void ICollection<KeyValuePair<String, Object>>.Clear()
        {
            throw new NotSupportedException();
        }
        bool ICollection<KeyValuePair<String, Object>>.Contains(KeyValuePair<String, Object> item)
        {
            throw new NotSupportedException();
        }
        bool IDictionary<String, Object>.ContainsKey(String key)
        {
            if (_properties.ContainsKey(key))
                return true;

            return _dynamicTypeDefinition.ContainsPropertyName(key);
        }
        void ICollection<KeyValuePair<String, Object>>.CopyTo(KeyValuePair<String, Object>[] array, int arrayIndex)
        {
            String[] keys = GetKeys();
            Object[] values = GetValues();
            for (int i = 0; i < keys.Length; i++)
                array[arrayIndex + i] = new KeyValuePair<String, Object>(keys[i], values[i]);
        }
        IEnumerator<KeyValuePair<String, Object>> IEnumerable<KeyValuePair<String, Object>>.GetEnumerator()
        {
            String[] keys = GetKeys();
            Object[] values = GetValues();
            for (int i = 0; i < keys.Length; i++)
                yield return new KeyValuePair<String, Object>(keys[i], values[i]);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<String, Object>>)this).GetEnumerator();
        }
        bool IDictionary<String, Object>.Remove(String key)
        {
            throw new NotSupportedException();
        }
        bool ICollection<KeyValuePair<String, Object>>.Remove(KeyValuePair<String, Object> item)
        {
            throw new NotSupportedException();
        }
        bool IDictionary<String, Object>.TryGetValue(String key, out Object value)
        {
            if (_properties.TryGetValue(key, out value))
                return true;

            String[] keys = GetKeys();
            for (int i = _properties.Count; i < keys.Length; i++)
                if (String.CompareOrdinal(keys[i], key) == 0)
                {
                    value = GetValues()[i];
                    return true;
                }

            return false;
        }

        Object IDictionary<String, Object>.this[String key]
        {
            get
            {
                if (((IDictionary<String, Object>)this).TryGetValue(key, out Object value))
                    return value;

                throw new KeyNotFoundException(key);
            }
            set
            {
                if (_properties.ContainsKey(key))
                    _properties[key] = value;
                else
                {
                    String[] keys = GetKeys();
                    for (int i = _properties.Count; i < keys.Length; i++)
                        if (String.CompareOrdinal(keys[i], key) == 0)
                        {
                            GetValues()[i] = value;
                            return;
                        }

                    throw new KeyNotFoundException(key);
                }
            }
        }
        ICollection<String> IDictionary<String, Object>.Keys => GetKeys();
        ICollection<Object> IDictionary<String, Object>.Values => GetValues();
        int ICollection<KeyValuePair<String, Object>>.Count => _properties.Count + _dynamicTypeDefinition.PropertyNames.Count;
        bool ICollection<KeyValuePair<String, Object>>.IsReadOnly => false;

#pragma warning disable 0649
        internal ICollection<DynamicType> CollectionNavigation01;
        internal ICollection<DynamicType> CollectionNavigation02;
        internal ICollection<DynamicType> CollectionNavigation03;
        internal ICollection<DynamicType> CollectionNavigation04;
        internal ICollection<DynamicType> CollectionNavigation05;
        internal ICollection<DynamicType> CollectionNavigation06;
        internal ICollection<DynamicType> CollectionNavigation07;
        internal ICollection<DynamicType> CollectionNavigation08;
        internal ICollection<DynamicType> CollectionNavigation09;
        internal ICollection<DynamicType> CollectionNavigation10;

        internal DynamicType SingleNavigation01;
        internal DynamicType SingleNavigation02;
        internal DynamicType SingleNavigation03;
        internal DynamicType SingleNavigation04;
        internal DynamicType SingleNavigation05;
        internal DynamicType SingleNavigation06;
        internal DynamicType SingleNavigation07;
        internal DynamicType SingleNavigation08;
        internal DynamicType SingleNavigation09;
        internal DynamicType SingleNavigation10;
#pragma warning restore 0649

        internal T ShadowPropertyGet01<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet02<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet03<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet04<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet05<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet06<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet07<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet08<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet09<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet10<T>() => GetShadowPropertyValue<T>();

        private T GetShadowPropertyValue<T>([CallerMemberName] String caller = null)
        {
            String propertyName = _dynamicTypeDefinition.GetShadowPropertyName(caller);
            return (T)_properties[propertyName];
        }
    }
}
