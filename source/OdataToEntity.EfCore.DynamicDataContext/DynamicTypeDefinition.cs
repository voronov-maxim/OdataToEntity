using System;
using System.Collections.Generic;
using System.Globalization;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinition
    {
        private int _collectionFieldIndex;
        private readonly Dictionary<String, String> _navigationPropertyNames;
        private int _singleFieldIndex;

        public DynamicTypeDefinition(Type dynamicTypeType, String entityName, String tableEdmName, bool isQueryType)
        {
            DynamicTypeType = dynamicTypeType;
            EntityName = entityName;
            TableEdmName = tableEdmName;
            IsQueryType = isQueryType;

            _navigationPropertyNames = new Dictionary<String, String>();
        }

        public String GetCollectionFiledName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String? fieldName))
                return fieldName;

            _collectionFieldIndex++;
            fieldName = "CollectionNavigation" + _collectionFieldIndex.ToString(CultureInfo.InvariantCulture);
            _navigationPropertyNames.Add(navigationPropertyName, fieldName);
            return fieldName;
        }
        public String GetSingleFieldName(String navigationPropertyName)
        {
            if (_navigationPropertyNames.TryGetValue(navigationPropertyName, out String? fieldName))
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
