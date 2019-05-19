using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinition
    {
        private int _collectionFieldIndex;
        private readonly Dictionary<String, String> _navigationPropertyNames;
        private int _shadowPropertyIndex;
        private readonly Dictionary<String, MethodInfo> _shadowPropertyGetMethodInfo;
        private readonly Dictionary<String, String> _shadowPropertyNames;
        private int _singleFieldIndex;

        public DynamicTypeDefinition(Type dynamicTypeType, String entityName, String tableName)
        {
            DynamicTypeType = dynamicTypeType;
            EntityName = entityName;
            TableName = tableName;

            _navigationPropertyNames = new Dictionary<String, String>();
            _shadowPropertyGetMethodInfo = new Dictionary<String, MethodInfo>();
            _shadowPropertyNames = new Dictionary<String, String>();
        }

        public bool ContainsPropertyName(String propertyName)
        {
            return _navigationPropertyNames.ContainsKey(propertyName);
        }
        public String GetCollectionFiledName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String fieldName))
                return fieldName;

            _collectionFieldIndex++;
            fieldName = "CollectionNavigation" + _collectionFieldIndex.ToString("D2");
            _navigationPropertyNames.Add(navigationPropertyName, fieldName);
            return fieldName;
        }
        public MethodInfo GetShadowPropertyGetMethodInfo(String propertyName, Type propertyType)
        {
            if (_shadowPropertyGetMethodInfo.TryGetValue(propertyName, out MethodInfo getMethodInfo))
                return getMethodInfo;

            if (_shadowPropertyIndex > 9)
                return null;

            _shadowPropertyIndex++;
            String shadowPropertyGetName = "ShadowPropertyGet" + _shadowPropertyIndex.ToString("D2");
            getMethodInfo = typeof(DynamicType).GetMethod(shadowPropertyGetName, BindingFlags.Instance | BindingFlags.NonPublic);
            getMethodInfo = getMethodInfo.GetGenericMethodDefinition().MakeGenericMethod(new Type[] { propertyType});

            _shadowPropertyGetMethodInfo.Add(propertyName, getMethodInfo);
            _shadowPropertyNames.Add(shadowPropertyGetName, propertyName);
            return getMethodInfo;
        }
        public String GetShadowPropertyName(String shadowPropertyName)
        {
            return _shadowPropertyNames[shadowPropertyName];
        }
        public String GetSingleFiledName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String fieldName))
                return fieldName;

            _singleFieldIndex++;
            fieldName = "SingleNavigation" + _singleFieldIndex.ToString("D2");
            _navigationPropertyNames.Add(navigationPropertyName, fieldName);
            return fieldName;
        }
        public bool TryGetValue(DynamicType dynamicType, String propertyName, out Object value)
        {
            if (_navigationPropertyNames.TryGetValue(propertyName, out String fieldName))
            {
                FieldInfo fieldInfo = typeof(DynamicType).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                value = fieldInfo.GetValue(dynamicType);
                return true;
            }

            value = null;
            return false;
        }
        public bool TrySetValue(DynamicType dynamicType, String propertyName, Object value)
        {
            if (_navigationPropertyNames.TryGetValue(propertyName, out String fieldName))
            {
                FieldInfo fieldInfo = typeof(DynamicType).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                fieldInfo.SetValue(dynamicType, value);
                return true;
            }

            return false;
        }

        public Type DynamicTypeType { get; }
        public String EntityName { get; }
        public IReadOnlyCollection<String> PropertyNames => _navigationPropertyNames.Keys;
        public String TableName { get; }
    }
}
