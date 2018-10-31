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

                IEdmNavigationProperty joinNavigationProperty = GetJoinNavigationProperty(typeInfo, join.DeclaringType);
                if (joinNavigationProperty == null)
                    continue;

                var targetNavigationProperty = (IEdmNavigationProperty)_entityTypeInfos[join.DeclaringType].EdmType.FindProperty(join.Name);
                if (targetNavigationProperty == null)
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
                EdmNavigationProperty edmManyToManyProperty = dependentInfo.EdmType.AddUnidirectionalNavigation(edmDependentInfo);

                var manyToManyJoinDescription = new ManyToManyJoinDescription(join.DeclaringType, joinNavigationProperty, targetNavigationProperty);
                _edmModel.SetAnnotationValue(edmManyToManyProperty, manyToManyJoinDescription);
            }
        }
        private static IEdmNavigationProperty GetJoinNavigationProperty(EntityTypeInfo typeInfo, Type joinClassType)
        {
            foreach (PropertyInfo propertyInfo in typeInfo.ClrType.GetProperties())
            {
                Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
                if (itemType == joinClassType)
                    foreach (IEdmNavigationProperty edmNavigationProperty in typeInfo.EdmType.NavigationProperties())
                        if (String.CompareOrdinal(edmNavigationProperty.Name, propertyInfo.Name) == 0)
                            return edmNavigationProperty;
            }

            return null;
        }
        private static IEnumerable<(PropertyInfo Many, PropertyInfo Join)> GetManyToManyInfo(OeEdmModelMetadataProvider metadataProvider, Type entityType)
        {
            var collectionProperties = new List<PropertyInfo>();
            foreach (PropertyInfo propertyInfo in entityType.GetProperties())
                if (!Parsers.OeExpressionHelper.IsPrimitiveType(propertyInfo.PropertyType) &&
                    Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType) != null)
                    collectionProperties.Add(propertyInfo);

            foreach (PropertyInfo propertyInfo in collectionProperties)
                if (metadataProvider.IsNotMapped(propertyInfo))
                {
                    Type itemType = Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType);
                    foreach (PropertyInfo propertyInfo2 in collectionProperties)
                        if (!metadataProvider.IsNotMapped(propertyInfo2))
                        {
                            Type itemType2 = Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo2.PropertyType);
                            PropertyInfo partnerProperty = GetPartnerProperty(metadataProvider, itemType, itemType2);
                            if (partnerProperty != null && itemType == partnerProperty.PropertyType)
                            {
                                yield return (propertyInfo, partnerProperty);
                                break;
                            }
                        }
                }
        }
        private static PropertyInfo GetPartnerProperty(OeEdmModelMetadataProvider metadataProvider, Type itemType, Type itemType2)
        {
            PropertyInfo partnerProperty = null;
            PropertyInfo otherSideProperty = null;
            foreach (PropertyInfo propertyInfo in itemType2.GetProperties())
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
                    if (Parsers.OeExpressionHelper.GetCollectionItemType(propertyInfo.PropertyType) == null)
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
