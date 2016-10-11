using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.ModelBuilder
{
    internal sealed class FKeyInfo
    {
        private readonly EntityTypeInfo _dependentInfo;
        private readonly PropertyDescriptor _dependentNavigationProperty;
        private readonly EdmMultiplicity _dependentMultiplicity;
        private readonly PropertyDescriptor[] _dependentStructuralProperties;
        private readonly EntityTypeInfo _principalInfo;
        private readonly EdmMultiplicity _principalMultiplicity;
        private readonly PropertyDescriptor _principalNavigationProperty;

        private FKeyInfo(EntityTypeInfo dependentInfo, PropertyDescriptor dependentNavigationProperty, PropertyDescriptor[] dependentStructuralProperties,
            EntityTypeInfo principalInfo, PropertyDescriptor principalNavigationProperty)
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
                bool isCollection = Parsers.OeExpressionHelper.GetCollectionItemType(principalNavigationProperty.PropertyType) != null;
                _principalNavigationProperty = principalNavigationProperty;
                _principalMultiplicity = GetEdmMultiplicity(principalNavigationProperty.PropertyType, null);
            }
        }

        public static FKeyInfo Create(Dictionary<Type, EntityTypeInfo> entityTypes, EntityTypeInfo dependentInfo, PropertyDescriptor dependentNavigationProperty)
        {
            Type clrType = Parsers.OeExpressionHelper.GetCollectionItemType(dependentNavigationProperty.PropertyType);
            if (clrType == null)
                clrType = dependentNavigationProperty.PropertyType;

            EntityTypeInfo principalInfo;
            if (!entityTypes.TryGetValue(clrType, out principalInfo))
                return null;

            PropertyDescriptor[] dependentStructuralProperties = GetDependentStructuralProperties(dependentInfo, dependentNavigationProperty);
            PropertyDescriptor principalNavigationProperty = GetPrincipalNavigationProperty(principalInfo, dependentInfo);
            if (dependentStructuralProperties.Length == 0 && principalNavigationProperty != null)
                return null;

            return new FKeyInfo(dependentInfo, dependentNavigationProperty, dependentStructuralProperties, principalInfo, principalNavigationProperty);
        }
        private static PropertyDescriptor[] GetDependentStructuralProperties(EntityTypeInfo dependentInfo, PropertyDescriptor dependentProperty)
        {
            var dependentProperties = new List<PropertyDescriptor>(1);
            PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(dependentInfo.ClrType);

            var fkey = (ForeignKeyAttribute)dependentProperty.Attributes[typeof(ForeignKeyAttribute)];
            if (fkey == null)
            {
                foreach (PropertyDescriptor propertyDescriptor in clrProperties)
                {
                    fkey = (ForeignKeyAttribute)propertyDescriptor.Attributes[typeof(ForeignKeyAttribute)];
                    if (fkey != null && String.Compare(fkey.Name, dependentProperty.Name, StringComparison.OrdinalIgnoreCase) == 0)
                        dependentProperties.Add(propertyDescriptor);
                }

                if (dependentProperties.Count == 0)
                {
                    String idName = dependentProperty.Name + "Id";
                    PropertyDescriptor clrProperty = clrProperties.Find(idName, true);
                    if (clrProperty != null)
                        dependentProperties.Add(clrProperty);
                }
            }
            else
            {
                PropertyDescriptor dependentProperty2 = clrProperties.Find(fkey.Name, true);
                if (dependentProperty2 != null)
                    dependentProperties.Add(dependentProperty2);
            }

            if (dependentProperties.Count == 1)
                return dependentProperties.ToArray();
            else
                return SortClrPropertyByOrder(dependentProperties).ToArray();
        }
        private static EdmMultiplicity GetEdmMultiplicity(Type propertyType, PropertyDescriptor[] dependentStructuralProperties)
        {
            if (Parsers.OeExpressionHelper.GetCollectionItemType(propertyType) != null)
                return EdmMultiplicity.Many;

            if (dependentStructuralProperties.Length == 0)
                return EdmMultiplicity.Unknown;

            foreach (PropertyDescriptor clrProperty in dependentStructuralProperties)
                if (PrimitiveTypeHelper.IsNullable(clrProperty.PropertyType))
                    return EdmMultiplicity.ZeroOrOne;

            return EdmMultiplicity.One;
        }
        private static PropertyDescriptor GetPrincipalNavigationProperty(EntityTypeInfo principalInfo, EntityTypeInfo dependentInfo)
        {
            foreach (PropertyDescriptor clrProperty in TypeDescriptor.GetProperties(principalInfo.ClrType))
                if (clrProperty.PropertyType == dependentInfo.ClrType ||
                    Parsers.OeExpressionHelper.GetCollectionItemType(clrProperty.PropertyType) == dependentInfo.ClrType)
                    return clrProperty;

            return null;
        }
        private static IEnumerable<PropertyDescriptor> SortClrPropertyByOrder(IEnumerable<PropertyDescriptor> clrProperties)
        {
            var propertyList = new List<Tuple<PropertyDescriptor, int>>(2);
            foreach (PropertyDescriptor clrProperty in clrProperties)
            {
                var column = (ColumnAttribute)clrProperty.Attributes[typeof(ColumnAttribute)];
                if (column == null)
                    return clrProperties;

                propertyList.Add(new Tuple<PropertyDescriptor, int>(clrProperty, column.Order));
            }
            return propertyList.OrderBy(t => t.Item2).Select(t => t.Item1);
        }

        public EntityTypeInfo DependentInfo => _dependentInfo;
        public EdmMultiplicity DependentMultiplicity => _dependentMultiplicity;
        public PropertyDescriptor DependentNavigationProperty => _dependentNavigationProperty;
        public PropertyDescriptor[] DependentStructuralProperties => _dependentStructuralProperties;
        public IEdmNavigationProperty EdmNavigationProperty { get; set; }
        public EntityTypeInfo PrincipalInfo => _principalInfo;
        public EdmMultiplicity PrincipalMultiplicity => _principalMultiplicity;
        public PropertyDescriptor PrincipalNavigationProperty => _principalNavigationProperty;
    }
}
