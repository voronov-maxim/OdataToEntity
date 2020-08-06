using Microsoft.OData.Edm;
using OdataToEntity.Infrastructure;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    internal sealed class FKeyInfo
    {
        private IEdmNavigationProperty? _edmNavigationProperty;

        private FKeyInfo(EntityTypeInfo dependentInfo, PropertyInfo? dependentNavigationProperty, PropertyInfo[] dependentStructuralProperties,
            EntityTypeInfo principalInfo, PropertyInfo? principalNavigationProperty, PropertyInfo[]? principalStructuralProperties)
        {
            DependentInfo = dependentInfo;
            DependentStructuralProperties = dependentStructuralProperties;
            DependentNavigationProperty = dependentNavigationProperty;
            PrincipalInfo = principalInfo;

            DependentMultiplicity = GetEdmMultiplicity(dependentNavigationProperty?.PropertyType, dependentStructuralProperties);

            if (principalNavigationProperty == null)
                PrincipalMultiplicity = EdmMultiplicity.Unknown;
            else
            {
                PrincipalNavigationProperty = principalNavigationProperty;
                PrincipalStructuralProperties = principalStructuralProperties;
                PrincipalMultiplicity = GetEdmMultiplicity(principalNavigationProperty.PropertyType, dependentStructuralProperties);
            }
        }

        private IEdmNavigationProperty AddBidirectionalNavigation(EdmNavigationPropertyInfo edmDependentInfo, EdmNavigationPropertyInfo edmPrincipalInfo)
        {
            IEdmNavigationProperty dependentNavigationProperty;
            if (DependentNavigationProperty is OeShadowPropertyInfo || PrincipalNavigationProperty is OeShadowPropertyInfo)
            {
                var dependentShadowProperty = (OeEdmNavigationShadowProperty)AddDependentNavigation(edmDependentInfo, true);
                var principalShadowProperty = (OeEdmNavigationShadowProperty)AddPrincipalNavigation(edmPrincipalInfo, true);

                dependentShadowProperty.SetPartner(principalShadowProperty);
                principalShadowProperty.SetPartner(dependentShadowProperty);

                dependentNavigationProperty = dependentShadowProperty;
            }
            else
            {
                dependentNavigationProperty = Microsoft.OData.Edm.EdmNavigationProperty.CreateNavigationPropertyWithPartner(edmDependentInfo, edmPrincipalInfo);
                DependentInfo.EdmType.AddProperty(dependentNavigationProperty);
                PrincipalInfo.EdmType.AddProperty(dependentNavigationProperty.Partner);
            }

            return dependentNavigationProperty;
        }
        private IEdmNavigationProperty AddDependentNavigation(EdmNavigationPropertyInfo edmDependentInfo, bool forceShadow)
        {
            IEdmNavigationProperty edmNavigationProperty = Microsoft.OData.Edm.EdmNavigationProperty.CreateNavigationProperty(DependentInfo.EdmType, edmDependentInfo);
            if (DependentNavigationProperty is OeShadowPropertyInfo || forceShadow)
            {
                if (DependentNavigationProperty == null)
                {
                    var shadowPropertyInfo = new OeShadowPropertyInfo(DependentInfo.ClrType, PrincipalInfo.ClrType, edmNavigationProperty.Name);
                    edmNavigationProperty = new OeEdmNavigationShadowProperty(edmNavigationProperty, shadowPropertyInfo);
                }
                else
                    edmNavigationProperty = new OeEdmNavigationShadowProperty(edmNavigationProperty, DependentNavigationProperty);
            }

            DependentInfo.EdmType.AddProperty(edmNavigationProperty);
            return edmNavigationProperty;
        }
        private IEdmNavigationProperty AddPrincipalNavigation(EdmNavigationPropertyInfo edmPrincipalInfo, bool forceShadow)
        {
            IEdmNavigationProperty edmNavigationProperty = Microsoft.OData.Edm.EdmNavigationProperty.CreateNavigationProperty(PrincipalInfo.EdmType, edmPrincipalInfo);
            if (PrincipalNavigationProperty is OeShadowPropertyInfo || forceShadow)
            {
                if (PrincipalNavigationProperty == null)
                {
                    Type propertyType = edmNavigationProperty.Type.IsCollection() ? typeof(ICollection<>).MakeGenericType(DependentInfo.ClrType) : DependentInfo.ClrType;
                    var shadowPropertyInfo = new OeShadowPropertyInfo(PrincipalInfo.ClrType, propertyType, edmNavigationProperty.Name);
                    edmNavigationProperty = new OeEdmNavigationShadowProperty(edmNavigationProperty, shadowPropertyInfo);
                }
                else
                    edmNavigationProperty = new OeEdmNavigationShadowProperty(edmNavigationProperty, PrincipalNavigationProperty);
            }

            PrincipalInfo.EdmType.AddProperty(edmNavigationProperty);
            return edmNavigationProperty;
        }
        public void BuildNavigationProperty()
        {
            EdmStructuralProperty[]? dependentEdmProperties = CreateEdmProperties(DependentInfo.EdmType, DependentStructuralProperties);
            IEnumerable<IEdmStructuralProperty>? principalEdmProperties;
            if (PrincipalStructuralProperties == null)
                principalEdmProperties = PrincipalInfo.EdmType.DeclaredKey;
            else
                principalEdmProperties = CreateEdmProperties(PrincipalInfo.EdmType, PrincipalStructuralProperties);

            IEdmNavigationProperty edmNavigationProperty;
            EdmNavigationPropertyInfo edmPrincipalInfo;
            if (DependentNavigationProperty == null)
            {
                if (PrincipalNavigationProperty == null)
                    throw new InvalidOperationException("If not set DependentNavigationProperty must set PrincipalNavigationProperty");

                edmPrincipalInfo = new EdmNavigationPropertyInfo()
                {
                    ContainsTarget = false,
                    Name = PrincipalNavigationProperty.Name,
                    DependentProperties = dependentEdmProperties,
                    OnDelete = EdmOnDeleteAction.None,
                    PrincipalProperties = principalEdmProperties,
                    Target = DependentInfo.EdmType,
                    TargetMultiplicity = PrincipalMultiplicity
                };
                edmNavigationProperty = AddPrincipalNavigation(edmPrincipalInfo, false);
            }
            else
            {
                var edmDependentInfo = new EdmNavigationPropertyInfo()
                {
                    ContainsTarget = false,
                    Name = DependentNavigationProperty.Name,
                    DependentProperties = dependentEdmProperties,
                    OnDelete = EdmOnDeleteAction.None,
                    PrincipalProperties = principalEdmProperties,
                    Target = PrincipalInfo.EdmType,
                    TargetMultiplicity = DependentMultiplicity
                };
                if (PrincipalNavigationProperty == null || PrincipalNavigationProperty == DependentNavigationProperty)
                    edmNavigationProperty = AddDependentNavigation(edmDependentInfo, false);
                else
                {
                    edmPrincipalInfo = new EdmNavigationPropertyInfo()
                    {
                        ContainsTarget = false,
                        Name = PrincipalNavigationProperty.Name,
                        DependentProperties = null,
                        OnDelete = EdmOnDeleteAction.None,
                        PrincipalProperties = principalEdmProperties,
                        Target = DependentInfo.EdmType,
                        TargetMultiplicity = PrincipalMultiplicity
                    };
                    edmNavigationProperty = AddBidirectionalNavigation(edmDependentInfo, edmPrincipalInfo);
                }
            }
            _edmNavigationProperty = edmNavigationProperty;
        }
        public static FKeyInfo? Create(OeEdmModelMetadataProvider metadataProvider,
            Dictionary<Type, EntityTypeInfo> entityTypes, EntityTypeInfo dependentInfo, PropertyInfo dependentNavigationProperty)
        {
            Type clrType = Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(dependentNavigationProperty.PropertyType) ?? dependentNavigationProperty.PropertyType;
            if (!entityTypes.TryGetValue(clrType, out EntityTypeInfo? principalInfo))
                return null;

            PropertyInfo[] dependentStructuralProperties = GetDependentStructuralProperties(metadataProvider, dependentInfo, dependentNavigationProperty);
            PropertyInfo? principalNavigationProperty = GetPrincipalNavigationProperty(metadataProvider, principalInfo, dependentInfo, dependentNavigationProperty);

            PropertyInfo[]? principalStructuralProperties = null;
            if (dependentStructuralProperties.Length == 0)
            {
                if (principalNavigationProperty != null)
                    return null;

                dependentStructuralProperties = metadataProvider.GetPrincipalToDependentWithoutDependent(dependentNavigationProperty);
                if (dependentStructuralProperties == null)
                    throw new InvalidOperationException("not found dependent structural property " + dependentInfo.ClrType.Name + "Id for navigation property " + dependentNavigationProperty.Name);

                principalStructuralProperties = metadataProvider.GetPrincipalStructuralProperties(dependentNavigationProperty);
                return new FKeyInfo(principalInfo, null, dependentStructuralProperties, dependentInfo, dependentNavigationProperty, principalStructuralProperties);
            }
            else
            {
                if (principalNavigationProperty != null)
                    principalStructuralProperties = metadataProvider.GetPrincipalStructuralProperties(principalNavigationProperty);
                return new FKeyInfo(dependentInfo, dependentNavigationProperty, dependentStructuralProperties, principalInfo, principalNavigationProperty, principalStructuralProperties);
            }
        }
        private static EdmStructuralProperty[]? CreateEdmProperties(EdmEntityType entitytType, IReadOnlyList<PropertyInfo> structuralProperties)
        {
            if (structuralProperties.Count == 0)
                return null;

            EdmStructuralProperty[] dependentEdmProperties;
            dependentEdmProperties = new EdmStructuralProperty[structuralProperties.Count];
            for (int i = 0; i < dependentEdmProperties.Length; i++)
                dependentEdmProperties[i] = (EdmStructuralProperty)entitytType.GetPropertyIgnoreCase(structuralProperties[i].Name);
            return dependentEdmProperties;
        }
        private static PropertyInfo[] GetDependentStructuralProperties(OeEdmModelMetadataProvider metadataProvider,
            EntityTypeInfo dependentInfo, PropertyInfo dependentProperty)
        {
            var dependentProperties = new List<PropertyInfo>();

            PropertyInfo[]? fkey = metadataProvider.GetForeignKey(dependentProperty);
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
                    PropertyInfo? clrProperty = dependentInfo.ClrType.GetPropertyIgnoreCaseOrNull(dependentProperty.Name + "id");
                    if (clrProperty != null)
                        dependentProperties.Add(clrProperty);
                }
            }
            else
                dependentProperties.AddRange(fkey);

            PropertyInfo[] dependentPropertyArray = dependentProperties.ToArray();
            metadataProvider.SortClrPropertyByOrder(dependentPropertyArray);
            return dependentPropertyArray;
        }
        private static EdmMultiplicity GetEdmMultiplicity(Type? propertyType, PropertyInfo[] dependentStructuralProperties)
        {
            if (propertyType != null && Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(propertyType) != null)
                return EdmMultiplicity.Many;

            if (dependentStructuralProperties.Length == 0)
                return EdmMultiplicity.Unknown;

            foreach (PropertyInfo clrProperty in dependentStructuralProperties)
                if (PrimitiveTypeHelper.IsNullable(clrProperty.PropertyType))
                    return EdmMultiplicity.ZeroOrOne;

            return EdmMultiplicity.One;
        }
        private static PropertyInfo? GetPrincipalNavigationProperty(OeEdmModelMetadataProvider metadataProvider,
            EntityTypeInfo principalInfo, EntityTypeInfo dependentInfo, PropertyInfo dependentNavigationProperty)
        {
            PropertyInfo? inverseProperty = metadataProvider.GetInverseProperty(dependentNavigationProperty);
            if (inverseProperty != null)
                return inverseProperty;

            foreach (PropertyInfo clrProperty in metadataProvider.GetProperties(principalInfo.ClrType))
                if (clrProperty.PropertyType == dependentInfo.ClrType ||
                    Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(clrProperty.PropertyType) == dependentInfo.ClrType)
                {
                    inverseProperty = metadataProvider.GetInverseProperty(clrProperty);
                    if (inverseProperty == null || inverseProperty == dependentNavigationProperty)
                        return clrProperty;
                }

            return null;
        }

        public EntityTypeInfo DependentInfo { get; }
        public EdmMultiplicity DependentMultiplicity { get; }
        public PropertyInfo? DependentNavigationProperty { get; }
        public PropertyInfo[] DependentStructuralProperties { get; }
        public IEdmNavigationProperty EdmNavigationProperty => _edmNavigationProperty ?? throw new InvalidOperationException("Invoke " + nameof(BuildNavigationProperty));
        public EntityTypeInfo PrincipalInfo { get; }
        public EdmMultiplicity PrincipalMultiplicity { get; }
        public PropertyInfo? PrincipalNavigationProperty { get; }
        public PropertyInfo[]? PrincipalStructuralProperties { get; }
    }
}
