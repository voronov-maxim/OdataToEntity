using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal readonly struct ManyToManyBuilder
    {
        private readonly IEdmModel _edmModel;
        private readonly Dictionary<Type, EntityTypeInfo> _entityTypeInfos;
        private readonly OeEdmModelMetadataProvider _metadataProvider;

        public ManyToManyBuilder(IEdmModel edmModel, OeEdmModelMetadataProvider metadataProvider, Dictionary<Type, EntityTypeInfo> entityTypeInfos)
        {
            _edmModel = edmModel;
            _metadataProvider = metadataProvider;
            _entityTypeInfos = entityTypeInfos;
        }

        public void Build(EntityTypeInfo typeInfo)
        {
            foreach ((PropertyInfo many, PropertyInfo join) in GetManyToManyInfo(_metadataProvider, typeInfo.ClrType))
            {
                if (many == null || many.DeclaringType != typeInfo.ClrType)
                    continue;

                IEdmNavigationProperty? joinNavigationProperty = GetJoinNavigationProperty(typeInfo, join.DeclaringType!);
                if (joinNavigationProperty == null)
                    continue;

                EntityTypeInfo principalInfo = _entityTypeInfos[join.PropertyType];
                EntityTypeInfo dependentInfo = _entityTypeInfos[many.DeclaringType];

                var edmDependentInfo = new EdmNavigationPropertyInfo()
                {
                    ContainsTarget = true,
                    Name = many.Name,
                    OnDelete = EdmOnDeleteAction.None,
                    PrincipalProperties = principalInfo.EdmType.DeclaredKey,
                    Target = principalInfo.EdmType,
                    TargetMultiplicity = EdmMultiplicity.Many
                };
                IEdmNavigationProperty edmManyToManyProperty;
                if (typeInfo.ClrType.GetProperty(many.Name) == null)
                {
                    IEdmNavigationProperty edmNavigationProperty = EdmNavigationProperty.CreateNavigationProperty(dependentInfo.EdmType, edmDependentInfo);
                    edmManyToManyProperty = new OeEdmNavigationShadowProperty(edmNavigationProperty, many);
                    dependentInfo.EdmType.AddProperty(edmManyToManyProperty);
                }
                else
                    edmManyToManyProperty = dependentInfo.EdmType.AddUnidirectionalNavigation(edmDependentInfo);

                var targetNavigationProperty = (IEdmNavigationProperty)_entityTypeInfos[join.DeclaringType!].EdmType.GetPropertyIgnoreCase(join.Name);
                var manyToManyJoinDescription = new ManyToManyJoinDescription(join.DeclaringType!, joinNavigationProperty, targetNavigationProperty);
                _edmModel.SetAnnotationValue(edmManyToManyProperty, manyToManyJoinDescription);
            }
        }
        private IEdmNavigationProperty? GetJoinNavigationProperty(EntityTypeInfo typeInfo, Type joinClassType)
        {
            foreach (PropertyInfo propertyInfo in _metadataProvider.GetProperties(typeInfo.ClrType))
            {
                Type? itemType = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType);
                if (itemType == joinClassType)
                    foreach (IEdmNavigationProperty edmNavigationProperty in typeInfo.EdmType.NavigationProperties())
                        if (String.CompareOrdinal(edmNavigationProperty.Name, propertyInfo.Name) == 0)
                            return edmNavigationProperty;
            }

            return null;
        }
        private static IEnumerable<(PropertyInfo Many, PropertyInfo Join)> GetManyToManyInfo(OeEdmModelMetadataProvider metadataProvider, Type entityType)
        {
            IReadOnlyList<PropertyInfo> properties = metadataProvider.GetManyToManyProperties(entityType);
            for (int i = 0; i < properties.Count; i++)
            {
                Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(properties[i].PropertyType);
                foreach (PropertyInfo property2 in metadataProvider.GetProperties(entityType))
                {
                    Type? itemType2 = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(property2.PropertyType);
                    if (itemType2 != null)
                    {
                        PropertyInfo? partnerProperty = GetPartnerProperty(metadataProvider, itemType, itemType2);
                        if (partnerProperty != null && itemType == partnerProperty.PropertyType)
                        {
                            yield return (properties[i], partnerProperty);
                            break;
                        }
                    }
                }
            }
        }
        private static PropertyInfo? GetPartnerProperty(OeEdmModelMetadataProvider metadataProvider, Type itemType, Type itemType2)
        {
            PropertyInfo? partnerProperty = null;
            PropertyInfo? otherSideProperty = null;
            foreach (PropertyInfo propertyInfo in metadataProvider.GetProperties(itemType2))
            {
                if (Parsers.OeExpressionHelper.IsPrimitiveType(propertyInfo.PropertyType))
                    continue;

                if (propertyInfo.PropertyType == itemType)
                {
                    if (partnerProperty == null)
                        partnerProperty = propertyInfo;
                    else
                    {
                        if (otherSideProperty != null)
                            return null;

                        if (metadataProvider.GetInverseProperty(partnerProperty) == null)
                            otherSideProperty = propertyInfo;
                        else if (metadataProvider.GetInverseProperty(propertyInfo) == null)
                        {
                            otherSideProperty = partnerProperty;
                            partnerProperty = propertyInfo;
                        }
                        else
                            return null;
                    }
                }
                else
                {
                    if (Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(propertyInfo.PropertyType) == null)
                    {
                        if (otherSideProperty != null)
                            return null;

                        otherSideProperty = propertyInfo;
                    }
                    else
                        return null;
                }
            }

            return partnerProperty;
        }
    }
}
