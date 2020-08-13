using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinition
    {
        private readonly struct ShadowPropertyDefinition
        {
            public readonly MethodInfo MethodGet;
            public readonly FieldInfo FieldInfo;

            public ShadowPropertyDefinition(MethodInfo methodGet, FieldInfo fieldInfo)
            {
                MethodGet = methodGet;
                FieldInfo = fieldInfo;
            }
        }

        private int _collectionFieldIndex;
        private readonly Dictionary<String, String> _navigationPropertyNames;
        private int _shadowPropertyIndex;
        private readonly Dictionary<String, ShadowPropertyDefinition> _shadowPropertyDefinitions;
        private readonly Dictionary<String, FieldInfo> _shadowPropertyFieldInfoByGetName;
        private int _singleFieldIndex;

        public DynamicTypeDefinition(Type dynamicTypeType, String entityName, String tableEdmName, bool isQueryType)
        {
            DynamicTypeType = dynamicTypeType;
            EntityName = entityName;
            TableEdmName = tableEdmName;
            IsQueryType = isQueryType;

            _navigationPropertyNames = new Dictionary<String, String>();
            _shadowPropertyDefinitions = new Dictionary<String, ShadowPropertyDefinition>();
            _shadowPropertyFieldInfoByGetName = new Dictionary<String, FieldInfo>();
        }

        private ShadowPropertyDefinition AddShadowPropertyDefinition(String propertyName, Type propertyType)
        {
            _shadowPropertyIndex++;

            String shadowPropertyIndex = _shadowPropertyIndex.ToString(CultureInfo.InvariantCulture);
            String shadowPropertyGetName = "ShadowPropertyGet" + shadowPropertyIndex;
            MethodInfo? getMethodInfo = typeof(Types.DynamicType).GetMethod(shadowPropertyGetName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (getMethodInfo == null)
                throw new InvalidOperationException(shadowPropertyGetName + " out of range, property name " + propertyName);

            getMethodInfo = getMethodInfo.GetGenericMethodDefinition().MakeGenericMethod(new Type[] { propertyType });

            String shadowPropertyFieldName = "ShadowProperty" + shadowPropertyIndex;
            FieldInfo? fieldInfo = typeof(Types.DynamicType).GetField(shadowPropertyFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
                throw new InvalidOperationException(shadowPropertyFieldName + " out of range, proeprty name " + propertyName);

            var shadowPropertyDefinition = new ShadowPropertyDefinition(getMethodInfo, fieldInfo);
            _shadowPropertyDefinitions.Add(propertyName, shadowPropertyDefinition);
            _shadowPropertyFieldInfoByGetName.Add(shadowPropertyGetName, fieldInfo);
            return shadowPropertyDefinition;
        }
        public FieldInfo AddShadowPropertyFieldInfo(String propertyName, Type propertyType)
        {
            if (_shadowPropertyDefinitions.TryGetValue(propertyName, out ShadowPropertyDefinition shadowPropertyDefinition))
                return shadowPropertyDefinition.FieldInfo;

            return AddShadowPropertyDefinition(propertyName, propertyType).FieldInfo;
        }
        public MethodInfo AddShadowPropertyGetMethodInfo(String propertyName, Type propertyType)
        {
            if (_shadowPropertyDefinitions.TryGetValue(propertyName, out ShadowPropertyDefinition shadowPropertyDefinition))
                return shadowPropertyDefinition.MethodGet;

            return AddShadowPropertyDefinition(propertyName, propertyType).MethodGet;
        }
        public String GetCollectionFiledName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String fieldName))
                return fieldName;

            _collectionFieldIndex++;
            fieldName = "CollectionNavigation" + _collectionFieldIndex.ToString(CultureInfo.InvariantCulture);
            _navigationPropertyNames.Add(navigationPropertyName, fieldName);
            return fieldName;
        }
        public FieldInfo GetShadowPropertyFieldInfo(String propertyName)
        {
            return _shadowPropertyDefinitions[propertyName].FieldInfo;
        }
        public FieldInfo GetShadowPropertyFieldInfoNameByGetName(String shadowPropertyGetName)
        {
            return _shadowPropertyFieldInfoByGetName[shadowPropertyGetName];
        }
        public String GetSingleFieldName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String fieldName))
                return fieldName;

            _singleFieldIndex++;
            fieldName = "SingleNavigation" + _singleFieldIndex.ToString(CultureInfo.InvariantCulture);
            _navigationPropertyNames.Add(navigationPropertyName, fieldName);
            return fieldName;
        }

        public Type DynamicTypeType { get; }
        public String EntityName { get; }
        public bool IsQueryType { get; }
        public String TableEdmName { get; }
    }
}
