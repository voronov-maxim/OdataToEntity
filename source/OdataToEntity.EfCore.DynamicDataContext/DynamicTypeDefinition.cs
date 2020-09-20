using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicTypeDefinition
    {
        private int _collectionFieldIndex;
        private readonly Dictionary<INavigation, (Type ClrType, String? FieldName)> _navigations;

        public DynamicTypeDefinition(Type dynamicTypeType, String entityName, String tableEdmName, bool isQueryType)
        {
            DynamicTypeType = dynamicTypeType;
            EntityName = entityName;
            TableEdmName = tableEdmName;
            IsQueryType = isQueryType;

            _navigations = new Dictionary<INavigation, (Type ClrType, String? FieldName)>();
        }

        internal String? AddNavigationProperty(INavigation navigation, Type clrType)
        {
            if (_navigations.TryGetValue(navigation, out (Type ClrType, String? FieldName) value))
                return value.FieldName;

            String? fieldName = null;
            if (navigation.IsCollection)
            {
                _collectionFieldIndex++;
                fieldName = "CollectionNavigation" + _collectionFieldIndex.ToString(CultureInfo.InvariantCulture);
            }
            _navigations.Add(navigation, (clrType, fieldName));
            return fieldName;
        }
        public Type GetNavigationPropertyClrType(INavigation navigation)
        {
            return _navigations[navigation].ClrType;
        }

        public Type DynamicTypeType { get; }
        public String EntityName { get; }
        public bool IsQueryType { get; }
        public String TableEdmName { get; }
    }
}
