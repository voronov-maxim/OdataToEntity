using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal sealed class FKeyInfo
    {
        private readonly EntityTypeInfo _dependentInfo;
        private readonly PropertyInfo _dependentNavigationProperty;
        private readonly EdmMultiplicity _dependentMultiplicity;
        private readonly PropertyInfo[] _dependentStructuralProperties;
        private readonly EntityTypeInfo _principalInfo;
        private readonly EdmMultiplicity _principalMultiplicity;
        private readonly PropertyInfo _principalNavigationProperty;

        private FKeyInfo(EntityTypeInfo dependentInfo, PropertyInfo dependentNavigationProperty, PropertyInfo[] dependentStructuralProperties,
            EntityTypeInfo principalInfo, PropertyInfo principalNavigationProperty)
        {
            _dependentInfo = dependentInfo;
            _dependentNavigationProperty = dependentNavigationProperty;
            _principalInfo = principalInfo;

            _dependentStructuralProperties = dependentStructuralProperties;
            _dependentMultiplicity = GetEdmMultiplicity(dependentNavigationProperty.PropertyType, dependentStructuralProperties);

            if (principalNavigationProperty == null)
                _principalMultiplicity = EdmMultiplicity.Unknown;
            else
            {
                _principalNavigationProperty = principalNavigationProperty;
                _principalMultiplicity = GetEdmMultiplicity(principalNavigationProperty.PropertyType, dependentStructuralProperties);
            }
        }

        public static FKeyInfo Create(OeEdmModelMetadataProvider metadataProvider,
            Dictionary<Type, EntityTypeInfo> entityTypes, EntityTypeInfo dependentInfo, PropertyInfo dependentNavigationProperty)
        {
            Type clrType = Parsers.OeExpressionHelper.GetCollectionItemType(dependentNavigationProperty.PropertyType);
            if (clrType == null)
                clrType = dependentNavigationProperty.PropertyType;

            if (!entityTypes.TryGetValue(clrType, out EntityTypeInfo principalInfo))
                return null;

            PropertyInfo[] dependentStructuralProperties = GetDependentStructuralProperties(metadataProvider, dependentInfo, dependentNavigationProperty);
            PropertyInfo principalNavigationProperty = GetPrincipalNavigationProperty(metadataProvider, principalInfo, dependentInfo, dependentNavigationProperty);
            if (dependentStructuralProperties.Length == 0 && principalNavigationProperty != null)
                return null;

            return new FKeyInfo(dependentInfo, dependentNavigationProperty, dependentStructuralProperties, principalInfo, principalNavigationProperty);
        }
        private static PropertyInfo[] GetDependentStructuralProperties(OeEdmModelMetadataProvider metadataProvider,
            EntityTypeInfo dependentInfo, PropertyInfo dependentProperty)
        {
            var dependentProperties = new List<PropertyInfo>();

            PropertyInfo[] fkey = metadataProvider.GetForeignKey(dependentProperty);
            if (fkey == null)
            {
                foreach (PropertyInfo propertyInfo in metadataProvider.GetProperties(dependentInfo.ClrType))
                {
                    fkey = metadataProvider.GetForeignKey(propertyInfo);
                    if (fkey != null && fkey.Length == 1 && fkey[0] == dependentProperty)
                        dependentProperties.Add(propertyInfo);
                }

                if (dependentProperties.Count == 0)
                {
                    PropertyInfo clrProperty = dependentInfo.ClrType.GetPropertyIgnoreCase(dependentProperty.Name + "Id");
                    if (clrProperty != null)
                        dependentProperties.Add(clrProperty);
                }
            }
            else
                dependentProperties.AddRange(fkey);

            return metadataProvider.SortClrPropertyByOrder(dependentProperties.ToArray());
        }
        private static EdmMultiplicity GetEdmMultiplicity(Type propertyType, PropertyInfo[] dependentStructuralProperties)
        {
            if (Parsers.OeExpressionHelper.GetCollectionItemType(propertyType) != null)
                return EdmMultiplicity.Many;

            if (dependentStructuralProperties.Length == 0)
                return EdmMultiplicity.Unknown;

            foreach (PropertyInfo clrProperty in dependentStructuralProperties)
                if (PrimitiveTypeHelper.IsNullable(clrProperty.PropertyType))
                    return EdmMultiplicity.ZeroOrOne;

            return EdmMultiplicity.One;
        }
        private static PropertyInfo GetPrincipalNavigationProperty(OeEdmModelMetadataProvider metadataProvider,
            EntityTypeInfo principalInfo, EntityTypeInfo dependentInfo, PropertyInfo dependentNavigationProperty)
        {
            PropertyInfo inverseProperty = metadataProvider.GetInverseProperty(dependentNavigationProperty);
            if (inverseProperty != null)
                return inverseProperty;

            foreach (PropertyInfo clrProperty in metadataProvider.GetProperties(principalInfo.ClrType))
                if (clrProperty.PropertyType == dependentInfo.ClrType ||
                    Parsers.OeExpressionHelper.GetCollectionItemType(clrProperty.PropertyType) == dependentInfo.ClrType)
                {
                    inverseProperty = metadataProvider.GetInverseProperty(clrProperty);
                    if (inverseProperty == null || inverseProperty == dependentNavigationProperty)
                        return clrProperty;
                }

            return null;
        }

        public EntityTypeInfo DependentInfo => _dependentInfo;
        public EdmMultiplicity DependentMultiplicity => _dependentMultiplicity;
        public PropertyInfo DependentNavigationProperty => _dependentNavigationProperty;
        public PropertyInfo[] DependentStructuralProperties => _dependentStructuralProperties;
        public IEdmNavigationProperty EdmNavigationProperty { get; set; }
        public EntityTypeInfo PrincipalInfo => _principalInfo;
        public EdmMultiplicity PrincipalMultiplicity => _principalMultiplicity;
        public PropertyInfo PrincipalNavigationProperty => _principalNavigationProperty;
    }
}
