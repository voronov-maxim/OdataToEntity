using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal sealed class EntityTypeInfo
    {
        private readonly Type _clrType;
        private readonly EdmEntityType _edmType;
        private readonly String _entitySetName;
        private readonly List<KeyValuePair<PropertyDescriptor, EdmStructuralProperty>> _keyProperties;
        private readonly List<FKeyInfo> _navigationClrProperties;

        public EntityTypeInfo(Type clrType, String entitySetName)
        {
            _clrType = clrType;
            _entitySetName = entitySetName;

            _edmType = new EdmEntityType(clrType.Namespace, clrType.Name);
            _keyProperties = new List<KeyValuePair<PropertyDescriptor, EdmStructuralProperty>>(1);
            _navigationClrProperties = new List<FKeyInfo>(1);
        }

        private void AddKeys()
        {
            if (_keyProperties.Count == 0)
            {
                PropertyDescriptorCollection clrPoperties = TypeDescriptor.GetProperties(_clrType);
                PropertyDescriptor key = clrPoperties.Find("id", true);
                if (key != null)
                {
                    var edmProperty = (EdmStructuralProperty)_edmType.Properties().Single(p => p.Name == key.Name);
                    _keyProperties.Add(new KeyValuePair<PropertyDescriptor, EdmStructuralProperty>(key, edmProperty));
                }
                else
                {
                    key = clrPoperties.Find(_clrType.Name + "id", true);
                    if (key == null)
                        throw new InvalidOperationException("Key property not matching");

                    var edmProperty = (EdmStructuralProperty)_edmType.Properties().Single(p => p.Name == key.Name);
                    _keyProperties.Add(new KeyValuePair<PropertyDescriptor, EdmStructuralProperty>(key, edmProperty));
                }
            }

            if (_keyProperties.Count == 1)
            {
                _edmType.AddKeys(_keyProperties[0].Value);
                return;
            }

            var keys = new Tuple<EdmStructuralProperty, int>[_keyProperties.Count];
            for (int i = 0; i < _keyProperties.Count; i++)
            {
                var column = (ColumnAttribute)_keyProperties[i].Key.Attributes[typeof(ColumnAttribute)];
                if (column == null)
                {
                    _edmType.AddKeys(_keyProperties.Select(p => p.Value));
                    return;
                }

                keys[i] = new Tuple<EdmStructuralProperty, int>(_keyProperties[i].Value, column.Order);
            }
            _edmType.AddKeys(keys.OrderBy(p => p.Item2).Select(p => p.Item1));
        }
        private void BuildProperty(Dictionary<Type, EntityTypeInfo> entityTypes,
            Dictionary<Type, EdmEnumType> enumTypes, Dictionary<Type, EdmComplexType> complexTypes, PropertyDescriptor clrProperty)
        {
            IEdmTypeReference typeRef = PrimitiveTypeHelper.GetPrimitiveTypeRef(clrProperty);
            if (typeRef == null)
            {
                EdmEnumType edmEnumType;
                Type underlyingType = null;
                if (clrProperty.PropertyType.GetTypeInfo().IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(clrProperty.PropertyType)) != null && underlyingType.GetTypeInfo().IsEnum)
                {
                    Type clrPropertyType = underlyingType ?? clrProperty.PropertyType;
                    if (!enumTypes.TryGetValue(clrPropertyType, out edmEnumType))
                    {
                        edmEnumType = CreateEdmEnumType(clrPropertyType);
                        enumTypes.Add(clrPropertyType, edmEnumType);
                    }
                    typeRef = new EdmEnumTypeReference(edmEnumType, underlyingType != null);
                }
                else
                {
                    EdmComplexType edmComplexType;
                    if (complexTypes.TryGetValue(clrProperty.PropertyType, out edmComplexType))
                        typeRef = new EdmComplexTypeReference(edmComplexType, clrProperty.PropertyType.GetTypeInfo().IsClass);
                    else
                    {
                        FKeyInfo fkeyInfo = FKeyInfo.Create(entityTypes, this, clrProperty);
                        if (fkeyInfo != null)
                            _navigationClrProperties.Add(fkeyInfo);
                        return;
                    }
                }
            }
            else
            {
                if (clrProperty.PropertyType == typeof(DateTime?) && enumTypes.ContainsKey(typeof(DateTime?))) //zzz
                {
                    var edmType = enumTypes[typeof(DateTime?)];
                    typeRef = new EdmEnumTypeReference(edmType, true);
                }
            }

            var edmProperty = new EdmStructuralProperty(_edmType, clrProperty.Name, typeRef);
            _edmType.AddProperty(edmProperty);
            if (clrProperty.Attributes[typeof(KeyAttribute)] != null)
                _keyProperties.Add(new KeyValuePair<PropertyDescriptor, EdmStructuralProperty>(clrProperty, edmProperty));
        }
        public void BuildProperties(Dictionary<Type, EntityTypeInfo> entityTypes,
            Dictionary<Type, EdmEnumType> enumTypes, Dictionary<Type, EdmComplexType> complexTypes)
        {
            foreach (PropertyDescriptor clrProperty in TypeDescriptor.GetProperties(_clrType))
                if (clrProperty.Attributes[typeof(NotMappedAttribute)] == null)
                    BuildProperty(entityTypes, enumTypes, complexTypes, clrProperty);
            AddKeys();
        }
        private static EdmEnumType CreateEdmEnumType(Type clrEnumType)
        {
            var edmEnumType = new EdmEnumType(clrEnumType.Namespace, clrEnumType.Name);
            foreach (Enum clrMember in Enum.GetValues(clrEnumType))
            {
                long value = Convert.ToInt64(clrMember, CultureInfo.InvariantCulture);
                var edmMember = new EdmEnumMember(edmEnumType, clrMember.ToString(), new EdmEnumMemberValue(value));
                edmEnumType.AddMember(edmMember);
            }
            return edmEnumType;
        }
        public override String ToString()
        {
            return _clrType.FullName;
        }

        public Type ClrType => _clrType;
        public EdmEntityType EdmType => _edmType;
        public String EntitySetName => _entitySetName;
        public IReadOnlyList<FKeyInfo> NavigationClrProperties => _navigationClrProperties;
    }
}