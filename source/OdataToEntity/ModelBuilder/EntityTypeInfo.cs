using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.Edm.Vocabularies.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal sealed class EntityTypeInfo
    {
        private readonly bool _isDbQuery;
        private readonly List<KeyValuePair<PropertyInfo, EdmStructuralProperty>> _keyProperties;
        private readonly OeEdmModelMetadataProvider _metadataProvider;
        private readonly List<FKeyInfo> _navigationClrProperties;

        public EntityTypeInfo(OeEdmModelMetadataProvider metadataProvider, Type clrType, EdmEntityType edmType, bool isRefModel, bool isDbQuery)
        {
            _metadataProvider = metadataProvider;
            ClrType = clrType;
            EdmType = edmType;
            IsRefModel = isRefModel;
            _isDbQuery = isDbQuery;

            _keyProperties = new List<KeyValuePair<PropertyInfo, EdmStructuralProperty>>(1);
            _navigationClrProperties = new List<FKeyInfo>();
        }

        private void AddDbQueryKeys()
        {
            EdmType.AddKeys(EdmType.StructuralProperties());
        }
        private void AddKeys()
        {
            if (_keyProperties.Count == 0)
            {
                PropertyInfo? key = ClrType.GetPropertyIgnoreCaseOrNull("id");
                if (key != null)
                {
                    var edmProperty = (EdmStructuralProperty)EdmType.GetPropertyIgnoreCase(key.Name);
                    _keyProperties.Add(new KeyValuePair<PropertyInfo, EdmStructuralProperty>(key, edmProperty));
                }
                else
                {
                    key = ClrType.GetPropertyIgnoreCaseOrNull(ClrType.Name + "id");
                    if (key == null)
                    {
                        if (EdmType.Key().Any() || ClrType.IsAbstract)
                            return;

                        throw new InvalidOperationException("Key property not matching");
                    }

                    var edmProperty = (EdmStructuralProperty)EdmType.GetPropertyIgnoreCase(key.Name);
                    _keyProperties.Add(new KeyValuePair<PropertyInfo, EdmStructuralProperty>(key, edmProperty));
                }
            }

            if (_keyProperties.Count == 1)
            {
                EdmType.AddKeys(_keyProperties[0].Value);
                return;
            }

            var keys = new ValueTuple<EdmStructuralProperty, int>[_keyProperties.Count];
            for (int i = 0; i < _keyProperties.Count; i++)
            {
                int order = _metadataProvider.GetOrder(_keyProperties[i].Key);
                if (order == -1)
                {
                    EdmType.AddKeys(_keyProperties.Select(p => p.Value));
                    return;
                }

                keys[i] = new ValueTuple<EdmStructuralProperty, int>(_keyProperties[i].Value, order);
            }
            EdmType.AddKeys(keys.OrderBy(p => p.Item2).Select(p => p.Item1));
        }
        public void BuildStructuralProperties(EdmModel edmModel, Dictionary<Type, EntityTypeInfo> entityTypes,
            Dictionary<Type, EdmEnumType> enumTypes, Dictionary<Type, EdmComplexType> complexTypes)
        {
            foreach (PropertyInfo clrProperty in _metadataProvider.GetProperties(ClrType))
                if (!_metadataProvider.IsNotMapped(clrProperty))
                {
                    EdmStructuralProperty? edmProperty = BuildStructuralProperty(entityTypes, enumTypes, complexTypes, clrProperty);
                    if (edmProperty != null && _metadataProvider.IsDatabaseGenerated(clrProperty))
                    {
                        var databaseGenerated = new EdmVocabularyAnnotation(edmProperty, CoreVocabularyModel.ComputedTerm, new EdmBooleanConstant(true));
                        edmModel.SetVocabularyAnnotation(databaseGenerated);
                    }
                }

            if (_isDbQuery)
                AddDbQueryKeys();
            else
                AddKeys();
        }
        private EdmStructuralProperty? BuildStructuralProperty(Dictionary<Type, EntityTypeInfo> entityTypes,
            Dictionary<Type, EdmEnumType> enumTypes, Dictionary<Type, EdmComplexType> complexTypes, PropertyInfo clrProperty)
        {
            bool isNullable = !_metadataProvider.IsRequired(clrProperty);
            IEdmTypeReference? typeRef = PrimitiveTypeHelper.GetPrimitiveTypeRef(clrProperty, isNullable);
            if (typeRef == null)
            {
                Type? underlyingType = null;
                if (clrProperty.PropertyType.IsEnum ||
                    (underlyingType = Nullable.GetUnderlyingType(clrProperty.PropertyType)) != null && underlyingType.IsEnum)
                {
                    Type clrPropertyType = underlyingType ?? clrProperty.PropertyType;
                    if (!enumTypes.TryGetValue(clrPropertyType, out EdmEnumType? edmEnumType))
                    {
                        edmEnumType = OeEdmModelBuilder.CreateEdmEnumType(clrPropertyType);
                        enumTypes.Add(clrPropertyType, edmEnumType);
                    }
                    typeRef = new EdmEnumTypeReference(edmEnumType, underlyingType != null);
                }
                else
                {
                    if (complexTypes.TryGetValue(clrProperty.PropertyType, out EdmComplexType? edmComplexType))
                        typeRef = new EdmComplexTypeReference(edmComplexType, clrProperty.PropertyType.IsClass);
                    else
                    {
                        FKeyInfo? fkeyInfo = FKeyInfo.Create(_metadataProvider, entityTypes, this, clrProperty);
                        if (fkeyInfo != null)
                            _navigationClrProperties.Add(fkeyInfo);
                        return null;
                    }
                }
            }
            else
            {
                if (clrProperty.PropertyType == typeof(DateTime?) && enumTypes.ContainsKey(typeof(DateTime?)))
                {
                    var edmType = enumTypes[typeof(DateTime?)];
                    typeRef = new EdmEnumTypeReference(edmType, true);
                }
            }

            EdmStructuralProperty edmProperty = clrProperty is Infrastructure.OeShadowPropertyInfo ?
                new OeEdmStructuralShadowProperty(EdmType, clrProperty.Name, typeRef, clrProperty) :
                new EdmStructuralProperty(EdmType, clrProperty.Name, typeRef);
            EdmType.AddProperty(edmProperty);
            if (_metadataProvider.IsKey(clrProperty))
                _keyProperties.Add(new KeyValuePair<PropertyInfo, EdmStructuralProperty>(clrProperty, edmProperty));

            return edmProperty;
        }
        public override String ToString()
        {
            return ClrType.FullName ?? ClrType.Name;
        }

        public Type ClrType { get; }
        public EdmEntityType EdmType { get; }
        public bool IsRefModel { get; }
        public IReadOnlyList<FKeyInfo> NavigationClrProperties => _navigationClrProperties;
    }
}